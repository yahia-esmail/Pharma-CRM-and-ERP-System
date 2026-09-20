using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Suppliers;

public class SupplierService(IAppDbContext db) : ISupplierService
{
    public async Task<PagedResult<SupplierListItemDto>> GetListAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = db.Suppliers.AsNoTracking().Where(s => !s.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(s => s.Name.Contains(request.Search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new { s.Id, s.Name, s.ContactName, s.Phone, s.Status })
            .ToListAsync(ct);

        var results = new List<SupplierListItemDto>(items.Count);
        foreach (var s in items)
        {
            var outstanding = await GetOutstandingAsync(s.Id, ct);
            results.Add(new SupplierListItemDto(s.Id, s.Name, s.ContactName, s.Phone, outstanding, s.Status));
        }

        return new PagedResult<SupplierListItemDto>
        {
            Items = results, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<SupplierDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Supplier), id);

        return new SupplierDetailDto(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.TaxRegistrationNumber,
            s.PaymentTermDays, s.Status);
    }

    public async Task<int> CreateAsync(SupplierSaveRequest request, CancellationToken ct = default)
    {
        var supplier = new Supplier();
        Apply(supplier, request);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task UpdateAsync(int id, SupplierSaveRequest request, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Supplier), id);

        Apply(supplier, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Supplier), id);

        supplier.Status = SupplierStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }

    public async Task<SupplierLedgerDto> GetLedgerAsync(int id, CancellationToken ct = default)
    {
        var exists = await db.Suppliers.AnyAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (!exists) throw new NotFoundException(nameof(Supplier), id);

        var receiptLines = await db.PurchaseReceiptLines.AsNoTracking()
            .Where(l => l.PurchaseReceipt.PurchaseOrder.SupplierId == id)
            .Select(l => new SupplierLedgerLineDto("Goods Receipt", l.PurchaseReceipt.ReceiptDateUtc,
                l.QuantityReceived * l.PurchaseOrderLine.UnitCost, $"PO #{l.PurchaseReceipt.PurchaseOrderId}"))
            .ToListAsync(ct);

        var payments = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.SupplierId == id)
            .Select(p => new SupplierLedgerLineDto("Payment", p.PaymentDateUtc, -p.Amount, p.ReferenceNumber))
            .ToListAsync(ct);

        var totalPurchased = receiptLines.Sum(l => l.Amount);
        var totalPaid = payments.Sum(l => -l.Amount);

        var lines = receiptLines.Concat(payments).OrderByDescending(l => l.DateUtc).ToList();

        return new SupplierLedgerDto(id, totalPurchased, totalPaid, totalPurchased - totalPaid, lines);
    }

    public async Task<PagedResult<SupplierPaymentDto>> GetPaymentsAsync(PagedRequest request, int? supplierId,
        CancellationToken ct = default)
    {
        var query = db.SupplierPayments.AsNoTracking().Where(p => !p.IsDeleted);
        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.PaymentDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new SupplierPaymentDto(p.Id, p.SupplierId, p.Supplier.Name, p.Amount, p.PaymentDateUtc,
                p.PaymentMethod, p.ReferenceNumber, p.Notes))
            .ToListAsync(ct);

        return new PagedResult<SupplierPaymentDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<int> RecordPaymentAsync(SupplierPaymentSaveRequest request, CancellationToken ct = default)
    {
        var exists = await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct);
        if (!exists) throw new NotFoundException(nameof(Supplier), request.SupplierId);

        if (request.Amount <= 0)
            throw new ValidationFailedException("Amount must be greater than zero.");

        var outstanding = await GetOutstandingAsync(request.SupplierId, ct);
        if (request.Amount > outstanding)
            throw new ValidationFailedException($"Payment amount ({request.Amount:C}) exceeds the outstanding payable ({outstanding:C}).");

        var payment = new SupplierPayment
        {
            SupplierId = request.SupplierId,
            Amount = request.Amount,
            PaymentDateUtc = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes
        };
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        return payment.Id;
    }

    private async Task<decimal> GetOutstandingAsync(int supplierId, CancellationToken ct)
    {
        var totalPurchased = await db.PurchaseReceiptLines
            .Where(l => l.PurchaseReceipt.PurchaseOrder.SupplierId == supplierId)
            .SumAsync(l => (decimal?)(l.QuantityReceived * l.PurchaseOrderLine.UnitCost), ct) ?? 0m;
        var totalPaid = await db.SupplierPayments.Where(p => p.SupplierId == supplierId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        return totalPurchased - totalPaid;
    }

    private static void Apply(Supplier supplier, SupplierSaveRequest request)
    {
        supplier.Name = request.Name;
        supplier.ContactName = request.ContactName;
        supplier.Phone = request.Phone;
        supplier.Email = request.Email;
        supplier.TaxRegistrationNumber = request.TaxRegistrationNumber;
        supplier.PaymentTermDays = request.PaymentTermDays;
        supplier.Status = request.Status;
    }
}
