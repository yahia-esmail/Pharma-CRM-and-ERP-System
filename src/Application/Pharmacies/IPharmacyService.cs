using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Pharmacies;

public interface IPharmacyService
{
    Task<PagedResult<PharmacyListItemDto>> GetListAsync(PagedRequest request, int? territoryId,
        int? representativeId, CancellationToken ct = default);

    Task<PharmacyDetailDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateAsync(PharmacySaveRequest request, CancellationToken ct = default);

    Task UpdateAsync(int id, PharmacySaveRequest request, CancellationToken ct = default);

    Task DeactivateAsync(int id, CancellationToken ct = default);

    /// <summary>Pharmacy account statement (spec 4.3) — running balance and aging, derived from Sales (and, from Phase 5, Collections).</summary>
    Task<PharmacyLedgerDto> GetLedgerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<PharmacyVisitDto>> GetVisitsAsync(int pharmacyId, CancellationToken ct = default);

    Task<int> AddVisitAsync(int representativeId, PharmacyVisitSaveRequest request, CancellationToken ct = default);
}
