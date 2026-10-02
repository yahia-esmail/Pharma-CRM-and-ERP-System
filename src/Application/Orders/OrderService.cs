using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Custody;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Shared.Common;
using PharmaERP.Shared.Security;

namespace PharmaERP.Application.Orders;

public class OrderService(IAppDbContext db, ICustodyService custodyService, INotificationService notificationService,
    IUserDirectoryService userDirectory, ICurrentUserService currentUser, IPharmacyBalanceCalculator balances) : IOrderService
{
    private const int MaxLinesPerOrder = 200;

    public async Task<PagedResult<OrderListItemDto>> GetListAsync(PagedRequest request, int? pharmacyId,
        int? representativeId, OrderStatus? status, CancellationToken ct = default)
    {
        // A representative only ever lists their own orders, whatever the caller passed.
        if (CallerIsRepresentativeOnly) representativeId = currentUser.RepresentativeId ?? -1;

        var query = db.Orders.AsNoTracking().Where(o => !o.IsDeleted);

        if (pharmacyId.HasValue) query = query.Where(o => o.PharmacyId == pharmacyId);
        if (representativeId.HasValue) query = query.Where(o => o.RepresentativeId == representativeId);
        if (status.HasValue) query = query.Where(o => o.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.OrderDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrderListItemDto(o.Id, o.PharmacyId, o.Pharmacy.Name, o.RepresentativeId,
                o.Representative.FullName, o.OrderDateUtc, o.Status,
                o.Lines.Where(l => !l.IsDeleted).Sum(l => (decimal?)l.Quantity * l.UnitPrice * (1 - l.DiscountPercent / 100m)) ?? 0m,
                o.RejectionReason, o.Lines.Count(l => !l.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<OrderListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<OrderDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Pharmacy)
            .Include(o => o.Representative)
            .Include(o => o.Lines.Where(l => !l.IsDeleted))
            .ThenInclude(l => l.Product)
            .Include(o => o.Sale)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), id);
        EnsureCallerMayAccess(order);

        return ToDetailDto(order);
    }

    public async Task<int> SaveDraftAsync(int representativeId, int? orderId, OrderSaveRequest request,
        CancellationToken ct = default)
    {
        var pharmacyExists = await db.Pharmacies.AnyAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct);
        if (!pharmacyExists) throw new NotFoundException(nameof(Pharmacy), request.PharmacyId);

        var lines = request.Lines ?? [];
        if (lines.Count > MaxLinesPerOrder)
            throw new ValidationFailedException($"An order can have at most {MaxLinesPerOrder} lines.");
        if (request.Submit && lines.Count == 0)
            throw new ValidationFailedException("Add at least one order line before submitting.");
        foreach (var line in lines) ValidateLine(line);

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var prices = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, p => p.UnitPrice, ct);
        if (productIds.FirstOrDefault(pid => !prices.ContainsKey(pid)) is var missing and > 0)
            throw new NotFoundException(nameof(Product), missing);

        if (request.PharmacyVisitId is { } visitId)
        {
            var visit = await db.PharmacyVisits.AsNoTracking().Where(v => v.Id == visitId)
                .Select(v => new { v.RepresentativeId, v.PharmacyId })
                .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(PharmacyVisit), visitId);
            if (visit.RepresentativeId != representativeId) throw new ForbiddenAccessException();
            if (visit.PharmacyId != request.PharmacyId)
                throw new ValidationFailedException("The visit linked to this order is at a different pharmacy.");
        }

        Order order;
        if (orderId is { } id)
        {
            order = await db.Orders.Include(o => o.Lines.Where(l => !l.IsDeleted))
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Order), id);
            if (order.RepresentativeId != representativeId) throw new ForbiddenAccessException();
            if (order.Status != OrderStatus.Draft)
                throw new ValidationFailedException("Only a draft order can be edited.");
            foreach (var existing in order.Lines) existing.IsDeleted = true;
        }
        else
        {
            order = new Order { RepresentativeId = representativeId, OrderDateUtc = DateTime.UtcNow, Status = OrderStatus.Draft };
            db.Orders.Add(order);
        }

        order.PharmacyId = request.PharmacyId;
        order.PharmacyVisitId = request.PharmacyVisitId ?? order.PharmacyVisitId;
        foreach (var line in lines)
        {
            db.OrderLines.Add(new OrderLine
            {
                Order = order,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                BonusQuantity = line.BonusQuantity,
                UnitPrice = prices[line.ProductId], // server-controlled snapshot — never trusts a client-supplied price
                DiscountPercent = line.DiscountPercent
            });
        }

        if (request.Submit)
        {
            order.Status = OrderStatus.Submitted;
            order.SubmittedAtUtc = DateTime.UtcNow;
            if (request.Location is { } fix)
            {
                if (double.IsNaN(fix.Latitude) || fix.Latitude is < -90 or > 90 || double.IsNaN(fix.Longitude) || fix.Longitude is < -180 or > 180)
                    throw new ValidationFailedException("Location coordinates are out of range.");
                order.SubmitLatitude = fix.Latitude;
                order.SubmitLongitude = fix.Longitude;
                order.SubmitAccuracyMeters = fix.AccuracyMeters is >= 0 ? fix.AccuracyMeters : null;
            }
        }

        await db.SaveChangesAsync(ct);   // header, lines and status together, or nothing
        if (request.Submit) await NotifyOrderAwaitingApprovalAsync(order, ct);
        return order.Id;
    }

    public async Task<int> CreateDraftAsync(int representativeId, OrderCreateRequest request, CancellationToken ct = default)
    {
        var pharmacyExists = await db.Pharmacies.AnyAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct);
        if (!pharmacyExists) throw new NotFoundException(nameof(Pharmacy), request.PharmacyId);

        var order = new Order
        {
            PharmacyId = request.PharmacyId,
            RepresentativeId = representativeId,
            OrderDateUtc = DateTime.UtcNow,
            Status = OrderStatus.Draft
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task UpdateAsync(int orderId, OrderUpdateRequest request, CancellationToken ct = default)
    {
        var order = await LoadEditableOrderAsync(orderId, ct);

        var pharmacyExists = await db.Pharmacies.AnyAsync(p => p.Id == request.PharmacyId && !p.IsDeleted, ct);
        if (!pharmacyExists) throw new NotFoundException(nameof(Pharmacy), request.PharmacyId);

        order.PharmacyId = request.PharmacyId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> AddLineAsync(int orderId, OrderLineSaveRequest request, CancellationToken ct = default)
    {
        var order = await LoadEditableOrderAsync(orderId, ct);

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        ValidateLine(request);

        var line = new OrderLine
        {
            OrderId = order.Id,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            BonusQuantity = request.BonusQuantity,
            UnitPrice = product.UnitPrice, // server-controlled snapshot — never trusts a client-supplied price
            DiscountPercent = request.DiscountPercent
        };
        db.OrderLines.Add(line);
        await db.SaveChangesAsync(ct);
        return line.Id;
    }

    public async Task UpdateLineAsync(int orderId, int lineId, OrderLineSaveRequest request, CancellationToken ct = default)
    {
        await LoadEditableOrderAsync(orderId, ct);

        var line = await db.OrderLines.FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(OrderLine), lineId);
        ValidateLine(request);

        if (line.ProductId != request.ProductId)
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Product), request.ProductId);
            line.ProductId = request.ProductId;
            line.UnitPrice = product.UnitPrice; // re-snapshot — never trusts a client-supplied price
        }

        line.Quantity = request.Quantity;
        line.BonusQuantity = request.BonusQuantity;
        line.DiscountPercent = request.DiscountPercent;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveLineAsync(int orderId, int lineId, CancellationToken ct = default)
    {
        await LoadEditableOrderAsync(orderId, ct);

        var line = await db.OrderLines.FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(OrderLine), lineId);

        line.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitAsync(int orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);
        EnsureCallerMayAccess(order);

        if (order.Status != OrderStatus.Draft)
            throw new ValidationFailedException("Only a draft order can be submitted for approval.");
        if (order.Lines.Count == 0)
            throw new ValidationFailedException("Add at least one order line before submitting.");

        order.Status = OrderStatus.Submitted;
        order.SubmittedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await NotifyOrderAwaitingApprovalAsync(order, ct);
    }

    public async Task ApproveAsync(int orderId, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Lines.Where(l => !l.IsDeleted))
            .Include(o => o.Pharmacy)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (order.Status != OrderStatus.Submitted)
            throw new ValidationFailedException("Only a submitted order can be approved.");

        var total = order.Lines.Sum(l => l.Quantity * l.UnitPrice * (1 - l.DiscountPercent / 100m));

        // Credit limit enforcement (spec 4.3) — blocked outright here; a later phase can make the
        // block-vs-warn choice configurable per pharmacy/organization.
        if (order.Pharmacy.CreditLimit > 0)
        {
            // Sales minus collections — counting sales alone would put every paying pharmacy over its limit eventually.
            var outstanding = (await balances.GetAsync(order.PharmacyId, ct)).Outstanding;
            if (outstanding + total > order.Pharmacy.CreditLimit)
                throw new ValidationFailedException(
                    $"Approving this order would exceed the pharmacy's credit limit ({order.Pharmacy.CreditLimit:C}). Current outstanding: {outstanding:C}.");
        }

        // Reserved Quantity (addendum 3.7): this system fulfills orders from a representative's own
        // custody (issued to them ahead of time via CustodyService.IssueAsync), not directly from
        // warehouse stock — DeliverAsync only ever deducts from custody. So "reserving stock" here means
        // reserving a slice of the rep's own custody balance against every other Approved-but-undelivered
        // order, which is exactly what would otherwise let two orders both get Approved against the same
        // units and only fail later, at Delivery.
        foreach (var group in order.Lines.GroupBy(l => l.ProductId))
        {
            var requested = group.Sum(l => l.Quantity + l.BonusQuantity);
            var available = await custodyService.GetAvailableToSellAsync(order.RepresentativeId, group.Key, ct);
            if (requested > available)
            {
                var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == group.Key, ct);
                throw new ValidationFailedException(
                    $"Approving this order would reserve {requested} unit(s) of '{product?.Name}', but the representative only has {available} available " +
                    "(after accounting for other approved-but-undelivered orders).");
            }
        }

        order.Status = OrderStatus.Approved;
        order.RejectionReason = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int orderId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationFailedException("A rejection reason is required.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (order.Status != OrderStatus.Submitted)
            throw new ValidationFailedException("Only a submitted order can be rejected.");

        order.Status = OrderStatus.Rejected;
        order.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DeliverAsync(int orderId, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);
        EnsureCallerMayAccess(order);

        if (order.Status != OrderStatus.Approved)
            throw new ValidationFailedException("Only an approved order can be delivered.");

        var total = order.Lines.Sum(l => l.Quantity * l.UnitPrice * (1 - l.DiscountPercent / 100m));

        order.Status = OrderStatus.Delivered;

        var sale = new Sale
        {
            OrderId = order.Id,
            PharmacyId = order.PharmacyId,
            RepresentativeId = order.RepresentativeId,
            SaleDateUtc = DateTime.UtcNow,
            TotalAmount = total
        };
        db.Sales.Add(sale);

        // Custody-balance enforcement (spec 4.5, wired up in Phase 4): a rep cannot sell more than they
        // currently hold. Bonus units leave stock too even though they're carried at zero value in the
        // line total, so they count toward the deducted quantity. Staged here, saved together with the
        // Order/Sale below as one unit of work.
        var linesByProduct = order.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity + l.BonusQuantity)))
            .ToList();
        await custodyService.DeductForSaleAsync(order.RepresentativeId, sale, linesByProduct, ct);

        await db.SaveChangesAsync(ct);
        return sale.Id;
    }

    public async Task CancelAsync(int orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);
        EnsureCallerMayAccess(order);

        if (order.Status is not (OrderStatus.Draft or OrderStatus.Submitted))
            throw new ValidationFailedException("Only a draft or submitted order can be cancelled — an approved order is already committed to delivery.");

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>"Order awaiting approval" (addendum 3.10) — notifies every Sales Manager plus the
    /// specific District Manager of the submitting representative's territory, if either is resolvable.
    /// A missing/unassigned territory or district manager just means fewer recipients, never an error.</summary>
    private async Task NotifyOrderAwaitingApprovalAsync(Order order, CancellationToken ct)
    {
        var recipientUserIds = new HashSet<string>(await userDirectory.GetUserIdsInRoleAsync(Roles.SalesManager, ct));

        var repTerritoryId = await db.Representatives.AsNoTracking()
            .Where(r => r.Id == order.RepresentativeId)
            .Select(r => r.TerritoryId)
            .FirstOrDefaultAsync(ct);

        if (repTerritoryId is { } territoryId)
        {
            var dmUserId = await db.Territories.AsNoTracking()
                .Where(t => t.Id == territoryId)
                .Select(t => t.DistrictManager != null ? t.DistrictManager.ApplicationUserId : null)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(dmUserId)) recipientUserIds.Add(dmUserId);
        }

        foreach (var userId in recipientUserIds)
        {
            await notificationService.CreateAsync(userId, NotificationTypes.OrderAwaitingApproval,
                $"Order #{order.Id} is awaiting approval.", nameof(Order), order.Id, ct);
        }
    }

    private async Task<Order> LoadEditableOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);
        EnsureCallerMayAccess(order);

        if (order.Status != OrderStatus.Draft)
            throw new ValidationFailedException("Only a draft order can be edited.");

        return order;
    }

    /// <summary>True for a field rep without a manager/back-office role: they may only see and change their own orders.</summary>
    private bool CallerIsRepresentativeOnly =>
        currentUser.IsInRole(Roles.Representative) && !currentUser.HasUnrestrictedAccess
        && !currentUser.IsInRole(Roles.DistrictManager) && !currentUser.IsInRole(Roles.Finance);

    private void EnsureCallerMayAccess(Order order)
    {
        if (CallerIsRepresentativeOnly && order.RepresentativeId != currentUser.RepresentativeId)
            throw new ForbiddenAccessException();
    }

    private static void ValidateLine(OrderLineSaveRequest line)
    {
        if (line.Quantity <= 0)
            throw new ValidationFailedException("Quantity must be greater than zero.");
        if (line.BonusQuantity < 0)
            throw new ValidationFailedException("Bonus quantity cannot be negative.");
        if (line.DiscountPercent is < 0 or > 100)
            throw new ValidationFailedException("Discount must be between 0% and 100%.");
    }

    private static OrderDetailDto ToDetailDto(Order order)
    {
        // Entry order — what the rep typed, line by line.
        var lines = order.Lines.OrderBy(l => l.Id).Select(l => new OrderLineDto(l.Id, l.ProductId, l.Product.Name, l.Quantity,
            l.BonusQuantity, l.UnitPrice, l.DiscountPercent, l.Quantity * l.UnitPrice * (1 - l.DiscountPercent / 100m))).ToList();

        return new OrderDetailDto(order.Id, order.PharmacyId, order.Pharmacy.Name, order.RepresentativeId,
            order.Representative.FullName, order.OrderDateUtc, order.Status, order.RejectionReason, lines,
            lines.Sum(l => l.LineTotal), order.Sale?.Id, order.SubmittedAtUtc, order.PharmacyVisitId);
    }
}
