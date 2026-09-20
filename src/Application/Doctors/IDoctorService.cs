using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Doctors;

public interface IDoctorService
{
    Task<PagedResult<DoctorListItemDto>> GetListAsync(PagedRequest request, string? specialty, string? city,
        int? classificationId, int? territoryId, int? representativeId, CancellationToken ct = default);

    Task<DoctorDetailDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<DuplicateDoctorMatch>> FindDuplicatesAsync(string fullName, string? phone,
        string? clinicOrHospital, CancellationToken ct = default);

    Task<int> CreateAsync(DoctorSaveRequest request, CancellationToken ct = default);

    Task UpdateAsync(int id, DoctorSaveRequest request, CancellationToken ct = default);

    /// <summary>Classification changes are logged separately per spec 4.1 ("who changed it, when, from what tier to what tier").</summary>
    Task ChangeClassificationAsync(int id, int? newClassificationId, CancellationToken ct = default);

    Task DeactivateAsync(int id, CancellationToken ct = default);

    Task MergeAsync(int survivingDoctorId, int duplicateDoctorId, CancellationToken ct = default);

    Task<IReadOnlyList<DoctorVisitDto>> GetVisitsAsync(int doctorId, CancellationToken ct = default);
    Task<int> AddVisitAsync(int representativeId, DoctorVisitSaveRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DoctorFollowUpDto>> GetFollowUpsAsync(int doctorId, CancellationToken ct = default);
    Task<int> AddFollowUpAsync(DoctorFollowUpSaveRequest request, CancellationToken ct = default);
    Task CloseFollowUpAsync(int followUpId, string? outcomeNotes, CancellationToken ct = default);
}
