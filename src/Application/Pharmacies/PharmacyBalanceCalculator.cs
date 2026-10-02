using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;

namespace PharmaERP.Application.Pharmacies;

public record SaleBalance(int SaleId, DateTime SaleDateUtc, decimal Amount, decimal Paid)
{
    public decimal Open => Amount - Paid;
}

public record PharmacyBalance(decimal TotalSales, decimal TotalCollected, IReadOnlyList<SaleBalance> Sales)
{
    /// <summary>What the pharmacy owes overall (negative = paid in advance).</summary>
    public decimal Outstanding => TotalSales - TotalCollected;

    public SaleBalance? Sale(int saleId) => Sales.FirstOrDefault(s => s.SaleId == saleId);
}

/// <summary>The one definition of what a pharmacy owes and on which invoices (ledger screen, credit-limit
/// check, collection validation). Collections are applied to invoices in two passes:
///   1. explicit allocations (CollectionAllocation, or the legacy single Collection.SaleId), capped at each
///      invoice's amount;
///   2. everything else — unallocated amounts and any excess — oldest invoice first.</summary>
public interface IPharmacyBalanceCalculator
{
    Task<PharmacyBalance> GetAsync(int pharmacyId, CancellationToken ct = default);
}

public class PharmacyBalanceCalculator(IAppDbContext db) : IPharmacyBalanceCalculator
{
    public async Task<PharmacyBalance> GetAsync(int pharmacyId, CancellationToken ct = default)
    {
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.PharmacyId == pharmacyId)
            .OrderBy(s => s.SaleDateUtc).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.SaleDateUtc, s.TotalAmount })
            .ToListAsync(ct);

        var collections = await db.Collections.AsNoTracking()
            .Where(c => c.PharmacyId == pharmacyId)
            .Select(c => new { c.Amount, c.SaleId, Allocations = c.Allocations.Select(a => new { a.SaleId, a.Amount }).ToList() })
            .ToListAsync(ct);

        var explicitBySale = new Dictionary<int, decimal>();
        foreach (var c in collections)
        {
            if (c.Allocations.Count > 0)
                foreach (var a in c.Allocations) explicitBySale[a.SaleId] = explicitBySale.GetValueOrDefault(a.SaleId) + a.Amount;
            else if (c.SaleId is { } saleId)
                explicitBySale[saleId] = explicitBySale.GetValueOrDefault(saleId) + c.Amount;
        }

        var totalCollected = collections.Sum(c => c.Amount);
        var paid = sales.ToDictionary(s => s.Id, s => Math.Min(s.TotalAmount, explicitBySale.GetValueOrDefault(s.Id)));

        var pool = totalCollected - paid.Values.Sum();
        foreach (var s in sales)
        {
            if (pool <= 0) break;
            var apply = Math.Min(pool, s.TotalAmount - paid[s.Id]);
            paid[s.Id] += apply;
            pool -= apply;
        }

        return new PharmacyBalance(sales.Sum(s => s.TotalAmount), totalCollected,
            sales.Select(s => new SaleBalance(s.Id, s.SaleDateUtc, s.TotalAmount, paid[s.Id])).ToList());
    }
}
