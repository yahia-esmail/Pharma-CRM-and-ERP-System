using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;

namespace PharmaERP.Application.Lists;

public class ListService(IAppDbContext db, ICurrentUserService currentUser) : IListService
{
    public async Task<IReadOnlyList<CustomerListSummaryDto>> GetListsAsync(CancellationToken ct = default)
    {
        var lists = await db.CustomerLists.AsNoTracking()
            .Where(l => !l.IsDeleted)
            .Include(l => l.OwnerRepresentative)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        var result = new List<CustomerListSummaryDto>(lists.Count);
        foreach (var l in lists)
        {
            var count = l.Mode == CustomerListMode.Static
                ? await db.CustomerListItems.CountAsync(i => i.CustomerListId == l.Id && !i.IsDeleted, ct)
                : (await ResolveDynamicMembersAsync(l, ct)).Count;

            result.Add(new CustomerListSummaryDto(l.Id, l.Name, l.Type, l.Mode, l.Status,
                l.OwnerRepresentative?.FullName, count));
        }
        return result;
    }

    public async Task<CustomerListDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var list = await db.CustomerLists.AsNoTracking()
            .Include(l => l.OwnerRepresentative)
            .Include(l => l.FilterTerritory)
            .Include(l => l.FilterClassification)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerList), id);

        var members = list.Mode == CustomerListMode.Static
            ? await GetStaticMembersAsync(id, ct)
            : await ResolveDynamicMembersAsync(list, ct);

        return new CustomerListDetailDto(list.Id, list.Name, list.Type, list.Mode, list.Status,
            list.OwnerRepresentativeId, list.OwnerRepresentative?.FullName,
            list.FilterTerritoryId, list.FilterTerritory?.Name, list.FilterClassificationId,
            list.FilterClassification?.Name, list.FilterSegment, list.FilterActiveOnly, members);
    }

    public async Task<int> CreateAsync(CustomerListSaveRequest request, CancellationToken ct = default)
    {
        var list = new CustomerList();
        Apply(list, request);
        db.CustomerLists.Add(list);
        await db.SaveChangesAsync(ct);
        return list.Id;
    }

    public async Task UpdateAsync(int id, CustomerListSaveRequest request, CancellationToken ct = default)
    {
        var list = await db.CustomerLists.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerList), id);

        Apply(list, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var list = await db.CustomerLists.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerList), id);

        list.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(int listId, int? doctorId, int? pharmacyId, CancellationToken ct = default)
    {
        var list = await db.CustomerLists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerList), listId);

        if (list.Mode != CustomerListMode.Static)
            throw new ValidationFailedException("Only Static lists accept manually-added members — a Dynamic list's membership is computed from its filter.");
        if (doctorId is null && pharmacyId is null)
            throw new ValidationFailedException("Select a doctor or a pharmacy to add.");

        var alreadyIn = await db.CustomerListItems.AnyAsync(i => i.CustomerListId == listId && !i.IsDeleted
            && ((doctorId != null && i.DoctorId == doctorId) || (pharmacyId != null && i.PharmacyId == pharmacyId)), ct);
        if (alreadyIn)
            throw new ValidationFailedException("This customer is already in the list.");

        db.CustomerListItems.Add(new CustomerListItem
        {
            CustomerListId = listId,
            DoctorId = doctorId,
            PharmacyId = pharmacyId,
            AddedAtUtc = DateTime.UtcNow,
            AddedByUserId = currentUser.UserId ?? "system"
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(int listId, int listItemId, CancellationToken ct = default)
    {
        var item = await db.CustomerListItems.FirstOrDefaultAsync(
            i => i.Id == listItemId && i.CustomerListId == listId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerListItem), listItemId);

        item.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BulkAssignResultDto> BulkAssignAsync(int listId, int toRepresentativeId, string? reason, CancellationToken ct = default)
    {
        var list = await db.CustomerLists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CustomerList), listId);

        var repExists = await db.Representatives.AnyAsync(r => r.Id == toRepresentativeId && !r.IsDeleted, ct);
        if (!repExists) throw new NotFoundException(nameof(Representative), toRepresentativeId);

        var members = list.Mode == CustomerListMode.Static
            ? await GetStaticMembersAsync(listId, ct)
            : await ResolveDynamicMembersAsync(list, ct);

        var assigned = 0;
        var logged = 0;
        foreach (var member in members)
        {
            var (didAssign, didLog) = await TransferOneAsync(member.Kind, member.EntityId, toRepresentativeId, reason, ct);
            if (didAssign) assigned++;
            if (didLog) logged++;
        }
        await db.SaveChangesAsync(ct);
        return new BulkAssignResultDto(assigned, logged);
    }

    public async Task TransferCustomerAsync(CustomerTransferRequest request, CancellationToken ct = default)
    {
        if (request.DoctorId is null && request.PharmacyId is null)
            throw new ValidationFailedException("Select a doctor or a pharmacy to transfer.");

        var repExists = await db.Representatives.AnyAsync(r => r.Id == request.ToRepresentativeId && !r.IsDeleted, ct);
        if (!repExists) throw new NotFoundException(nameof(Representative), request.ToRepresentativeId);

        var kind = request.DoctorId is not null ? "Doctor" : "Pharmacy";
        var entityId = request.DoctorId ?? request.PharmacyId!.Value;

        var (didAssign, _) = await TransferOneAsync(kind, entityId, request.ToRepresentativeId, request.Reason, ct);
        if (!didAssign)
            throw new ValidationFailedException("This customer is already assigned to that representative.");

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerTransferLogDto>> GetTransferLogAsync(int? doctorId, int? pharmacyId, CancellationToken ct = default)
    {
        var query = db.CustomerTransferLogs.AsNoTracking().Where(t => !t.IsDeleted)
            .Include(t => t.Doctor).Include(t => t.Pharmacy)
            .Include(t => t.FromRepresentative).Include(t => t.ToRepresentative)
            .AsQueryable();

        if (doctorId.HasValue) query = query.Where(t => t.DoctorId == doctorId);
        if (pharmacyId.HasValue) query = query.Where(t => t.PharmacyId == pharmacyId);

        return await query
            .OrderByDescending(t => t.TransferDateUtc)
            .Select(t => new CustomerTransferLogDto(t.Id, t.DoctorId != null ? "Doctor" : "Pharmacy",
                (t.DoctorId ?? t.PharmacyId)!.Value,
                t.DoctorId != null ? t.Doctor!.FullName : t.Pharmacy!.Name,
                t.FromRepresentativeId, t.FromRepresentative.FullName,
                t.ToRepresentativeId, t.ToRepresentative.FullName,
                t.TransferDateUtc, t.Reason, t.ApprovedByUserId))
            .ToListAsync(ct);
    }

    /// <summary>Core of both single-customer Transfer and list BulkAssign: reassigns PrimaryRepresentativeId
    /// and, only when the customer previously had a different representative, appends an immutable
    /// CustomerTransferLog row (addendum 3.1's "never rewrites history" rule — past visits/orders/sales
    /// already reference whichever representative recorded them, so nothing else needs to change).</summary>
    private async Task<(bool didAssign, bool didLog)> TransferOneAsync(string kind, int entityId, int toRepresentativeId,
        string? reason, CancellationToken ct)
    {
        int? fromRepresentativeId;
        if (kind == "Doctor")
        {
            var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == entityId && !d.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Doctor), entityId);
            fromRepresentativeId = doctor.PrimaryRepresentativeId;
            if (fromRepresentativeId == toRepresentativeId) return (false, false);
            doctor.PrimaryRepresentativeId = toRepresentativeId;
        }
        else
        {
            var pharmacy = await db.Pharmacies.FirstOrDefaultAsync(p => p.Id == entityId && !p.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Pharmacy), entityId);
            fromRepresentativeId = pharmacy.PrimaryRepresentativeId;
            if (fromRepresentativeId == toRepresentativeId) return (false, false);
            pharmacy.PrimaryRepresentativeId = toRepresentativeId;
        }

        if (fromRepresentativeId is not { } fromId)
            return (true, false); // first-ever assignment — nothing to log a handover from

        db.CustomerTransferLogs.Add(new CustomerTransferLog
        {
            DoctorId = kind == "Doctor" ? entityId : null,
            PharmacyId = kind == "Pharmacy" ? entityId : null,
            FromRepresentativeId = fromId,
            ToRepresentativeId = toRepresentativeId,
            TransferDateUtc = DateTime.UtcNow,
            Reason = reason,
            ApprovedByUserId = currentUser.UserId ?? "system"
        });
        return (true, true);
    }

    private async Task<List<CustomerListMemberDto>> GetStaticMembersAsync(int listId, CancellationToken ct)
    {
        var items = await db.CustomerListItems.AsNoTracking()
            .Where(i => i.CustomerListId == listId && !i.IsDeleted)
            .Include(i => i.Doctor).ThenInclude(d => d!.PrimaryRepresentative)
            .Include(i => i.Pharmacy).ThenInclude(p => p!.PrimaryRepresentative)
            .ToListAsync(ct);

        return items.Select(i => i.Doctor is { } d
                ? new CustomerListMemberDto(i.Id, "Doctor", d.Id, d.FullName, d.City, d.PrimaryRepresentativeId, d.PrimaryRepresentative?.FullName)
                : new CustomerListMemberDto(i.Id, "Pharmacy", i.Pharmacy!.Id, i.Pharmacy.Name, i.Pharmacy.City,
                    i.Pharmacy.PrimaryRepresentativeId, i.Pharmacy.PrimaryRepresentative?.FullName))
            .ToList();
    }

    private async Task<List<CustomerListMemberDto>> ResolveDynamicMembersAsync(CustomerList list, CancellationToken ct)
    {
        var members = new List<CustomerListMemberDto>();

        if (list.Type is CustomerListType.DoctorList or CustomerListType.TerritoryList)
        {
            var query = db.Doctors.AsNoTracking().Where(d => !d.IsDeleted);
            if (list.FilterTerritoryId.HasValue) query = query.Where(d => d.TerritoryId == list.FilterTerritoryId);
            if (list.Type == CustomerListType.DoctorList && list.FilterClassificationId.HasValue)
                query = query.Where(d => d.ClassificationId == list.FilterClassificationId);
            if (list.FilterActiveOnly) query = query.Where(d => d.Status == DoctorStatus.Active);

            var doctors = await query
                .Select(d => new CustomerListMemberDto(null, "Doctor", d.Id, d.FullName, d.City,
                    d.PrimaryRepresentativeId, d.PrimaryRepresentative != null ? d.PrimaryRepresentative.FullName : null))
                .ToListAsync(ct);
            members.AddRange(doctors);
        }

        if (list.Type is CustomerListType.PharmacyList or CustomerListType.TerritoryList)
        {
            var query = db.Pharmacies.AsNoTracking().Where(p => !p.IsDeleted);
            if (list.FilterTerritoryId.HasValue) query = query.Where(p => p.TerritoryId == list.FilterTerritoryId);
            if (list.Type == CustomerListType.PharmacyList && !string.IsNullOrWhiteSpace(list.FilterSegment))
                query = query.Where(p => p.Segment == list.FilterSegment);
            if (list.FilterActiveOnly) query = query.Where(p => p.Status == PharmacyStatus.Active);

            var pharmacies = await query
                .Select(p => new CustomerListMemberDto(null, "Pharmacy", p.Id, p.Name, p.City,
                    p.PrimaryRepresentativeId, p.PrimaryRepresentative != null ? p.PrimaryRepresentative.FullName : null))
                .ToListAsync(ct);
            members.AddRange(pharmacies);
        }

        return members;
    }

    private static void Apply(CustomerList list, CustomerListSaveRequest request)
    {
        list.Name = request.Name;
        list.Type = request.Type;
        list.Mode = request.Mode;
        list.OwnerRepresentativeId = request.OwnerRepresentativeId;
        list.FilterTerritoryId = request.FilterTerritoryId;
        list.FilterClassificationId = request.FilterClassificationId;
        list.FilterSegment = request.FilterSegment;
        list.FilterActiveOnly = request.FilterActiveOnly;
    }
}
