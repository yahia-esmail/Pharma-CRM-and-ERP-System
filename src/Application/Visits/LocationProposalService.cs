using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Visits;

public interface ILocationProposalService
{
    Task<LocationProposalResult> ProposeForDoctorAsync(int representativeId, int doctorId, LocationProposalRequest request, CancellationToken ct = default);
    Task<LocationProposalResult> ProposeForPharmacyAsync(int representativeId, int pharmacyId, LocationProposalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LocationProposalDto>> GetAsync(LocationProposalStatus? status, CancellationToken ct = default);
    Task ApproveAsync(int proposalId, string reviewerUserId, string? note, CancellationToken ct = default);
    Task RejectAsync(int proposalId, string reviewerUserId, string? note, CancellationToken ct = default);
}

/// <summary>Completes customer coordinates from the field (plan 7.9). See <see cref="CustomerLocationProposal"/>
/// for the auto-apply rule. Applying a location edits the Doctor/Pharmacy row itself, so it goes through the
/// audit trail like any other master-data change.</summary>
public class LocationProposalService(IAppDbContext db, IOptions<VisitValidationOptions> options, TimeProvider time)
    : ILocationProposalService
{
    public const string SystemReviewer = "system:auto-apply";

    public async Task<LocationProposalResult> ProposeForDoctorAsync(int representativeId, int doctorId,
        LocationProposalRequest request, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);
        var proposal = await CreateAsync(representativeId, request, doctor.Latitude, doctor.Longitude, ct);
        proposal.DoctorId = doctorId;
        if (proposal.Status == LocationProposalStatus.AutoApplied)
            (doctor.Latitude, doctor.Longitude) = (proposal.Latitude, proposal.Longitude);
        return await SaveAsync(proposal, ct);
    }

    public async Task<LocationProposalResult> ProposeForPharmacyAsync(int representativeId, int pharmacyId,
        LocationProposalRequest request, CancellationToken ct = default)
    {
        var pharmacy = await db.Pharmacies.FirstOrDefaultAsync(p => p.Id == pharmacyId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Pharmacy), pharmacyId);
        var proposal = await CreateAsync(representativeId, request, pharmacy.Latitude, pharmacy.Longitude, ct);
        proposal.PharmacyId = pharmacyId;
        if (proposal.Status == LocationProposalStatus.AutoApplied)
            (pharmacy.Latitude, pharmacy.Longitude) = (proposal.Latitude, proposal.Longitude);
        return await SaveAsync(proposal, ct);
    }

    public async Task<IReadOnlyList<LocationProposalDto>> GetAsync(LocationProposalStatus? status, CancellationToken ct = default)
    {
        var rows = await db.CustomerLocationProposals.AsNoTracking()
            .Where(p => status == null || p.Status == status)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id, p.DoctorId, p.PharmacyId,
                CustomerName = p.Doctor != null ? p.Doctor.FullName : p.Pharmacy != null ? p.Pharmacy.Name : "",
                p.RepresentativeId, RepresentativeName = p.Representative.FullName,
                p.Latitude, p.Longitude, p.AccuracyMeters, p.CapturedAtUtc, p.PreviousLatitude, p.PreviousLongitude,
                p.Note, p.Status, p.CreatedAtUtc
            })
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(p => new LocationProposalDto(p.Id, p.DoctorId is not null ? "Doctor" : "Pharmacy",
                p.DoctorId ?? p.PharmacyId!.Value, p.CustomerName, p.RepresentativeId, p.RepresentativeName,
                p.Latitude, p.Longitude, p.AccuracyMeters, p.CapturedAtUtc, p.PreviousLatitude, p.PreviousLongitude,
                p.PreviousLatitude is { } lat && p.PreviousLongitude is { } lon ? Math.Round(Haversine(lat, lon, p.Latitude, p.Longitude)) : null,
                p.Note, p.Status, p.CreatedAtUtc))
            .ToList();
    }

    public async Task ApproveAsync(int proposalId, string reviewerUserId, string? note, CancellationToken ct = default)
    {
        var proposal = await LoadPendingAsync(proposalId, ct);
        if (proposal.Doctor is { } doctor) (doctor.Latitude, doctor.Longitude) = (proposal.Latitude, proposal.Longitude);
        if (proposal.Pharmacy is { } pharmacy) (pharmacy.Latitude, pharmacy.Longitude) = (proposal.Latitude, proposal.Longitude);
        Review(proposal, LocationProposalStatus.Approved, reviewerUserId, note);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int proposalId, string reviewerUserId, string? note, CancellationToken ct = default)
    {
        var proposal = await LoadPendingAsync(proposalId, ct);
        Review(proposal, LocationProposalStatus.Rejected, reviewerUserId, note);
        await db.SaveChangesAsync(ct);
    }

    private async Task<CustomerLocationProposal> CreateAsync(int representativeId, LocationProposalRequest request,
        double? currentLatitude, double? currentLongitude, CancellationToken ct)
    {
        if (double.IsNaN(request.Latitude) || request.Latitude is < -90 or > 90 || double.IsNaN(request.Longitude) || request.Longitude is < -180 or > 180)
            throw new ValidationFailedException("Location coordinates are out of range.");
        if (request.AccuracyMeters is < 0 or > 1000)
            throw new ValidationFailedException("Location accuracy must be between 0 and 1000 m — take a better fix and try again.");
        if (!await db.Representatives.AnyAsync(r => r.Id == representativeId && !r.IsDeleted, ct))
            throw new NotFoundException(nameof(Representative), representativeId);

        var now = time.GetUtcNow().UtcDateTime;
        var autoApply = currentLatitude is null && currentLongitude is null
            && request.AccuracyMeters <= options.Value.AutoApplyLocationMaxAccuracyMeters;

        return new CustomerLocationProposal
        {
            RepresentativeId = representativeId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            CapturedAtUtc = request.CapturedAtUtc is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : now,
            PreviousLatitude = currentLatitude,
            PreviousLongitude = currentLongitude,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(request.Note.Trim().Length, 500)],
            CreatedAtUtc = now,
            Status = autoApply ? LocationProposalStatus.AutoApplied : LocationProposalStatus.Pending,
            ReviewedByUserId = autoApply ? SystemReviewer : null,
            ReviewedAtUtc = autoApply ? now : null
        };
    }

    private async Task<LocationProposalResult> SaveAsync(CustomerLocationProposal proposal, CancellationToken ct)
    {
        db.CustomerLocationProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
        return new LocationProposalResult(proposal.Id, proposal.Status);
    }

    private async Task<CustomerLocationProposal> LoadPendingAsync(int proposalId, CancellationToken ct)
    {
        var proposal = await db.CustomerLocationProposals.Include(p => p.Doctor).Include(p => p.Pharmacy)
            .FirstOrDefaultAsync(p => p.Id == proposalId, ct)
            ?? throw new NotFoundException(nameof(CustomerLocationProposal), proposalId);
        if (proposal.Status != LocationProposalStatus.Pending)
            throw new ValidationFailedException("This proposal has already been reviewed.");
        return proposal;
    }

    private void Review(CustomerLocationProposal proposal, LocationProposalStatus status, string reviewerUserId, string? note)
    {
        proposal.Status = status;
        proposal.ReviewedByUserId = reviewerUserId;
        proposal.ReviewedAtUtc = time.GetUtcNow().UtcDateTime;
        proposal.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 2 * 6_371_000 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
