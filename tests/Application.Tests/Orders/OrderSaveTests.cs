using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Orders;
using PharmaERP.Application.Visits;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Shared.Security;

namespace PharmaERP.Application.Tests.Orders;

public class OrderSaveTests : IDisposable
{
    private const int RepId = 7, OtherRepId = 8;
    private readonly ApplicationDbContext _db = TestDb.Create();
    private Pharmacy _pharmacy = null!, _otherPharmacy = null!;
    private Product _concor = null!, _augmentin = null!;

    public OrderSaveTests() => Seed();

    private void Seed()
    {
        _pharmacy = new Pharmacy { Name = "Tahrir Pharmacy", CreatedByUserId = "t" };
        _otherPharmacy = new Pharmacy { Name = "Garden City Pharmacy", CreatedByUserId = "t" };
        _concor = new Product { Sku = "CONC-5", Name = "Concor 5mg", UnitOfMeasure = "Box", UnitPrice = 85, CreatedByUserId = "t" };
        _augmentin = new Product { Sku = "AUGM-1G", Name = "Augmentin 1g", UnitOfMeasure = "Box", UnitPrice = 95, CreatedByUserId = "t" };
        _db.AddRange(
            new Representative { Id = RepId, EmployeeCode = "R7", FullName = "Yahia", CreatedByUserId = "t" },
            new Representative { Id = OtherRepId, EmployeeCode = "R8", FullName = "Sara", CreatedByUserId = "t" },
            _pharmacy, _otherPharmacy, _concor, _augmentin);
        _db.SaveChanges();
    }

    private OrderService ServiceFor(int? representativeId, params string[] roles) =>
        new(_db, null!, new NotificationService(_db, TestCalendar.Cairo()), new NoUsers(), new Caller(representativeId, roles));

    private OrderService Rep => ServiceFor(RepId, Roles.Representative);

    private OrderSaveRequest Request(bool submit = false, params (Product Product, int Qty, decimal Discount)[] lines) => new()
    {
        PharmacyId = _pharmacy.Id,
        Submit = submit,
        Lines = lines.Select(l => new OrderLineSaveRequest { ProductId = l.Product.Id, Quantity = l.Qty, DiscountPercent = l.Discount }).ToList()
    };

    [Fact]
    public async Task Whole_order_is_created_and_submitted_in_one_request_priced_by_the_server()
    {
        var request = Request(submit: true, (_concor, 10, 5), (_augmentin, 2, 0));
        request.Location = new VisitFix { Latitude = 30.045, Longitude = 31.2365, AccuracyMeters = 12 };

        var id = await Rep.SaveDraftAsync(RepId, null, request);

        var order = await Rep.GetByIdAsync(id);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.NotNull(order.SubmittedAtUtc);
        Assert.Equal([85m, 95m], order.Lines.Select(l => l.UnitPrice));
        Assert.Equal(10 * 85 * 0.95m + 2 * 95, order.TotalAmount);
        var stored = await _db.Orders.SingleAsync();
        Assert.Equal((30.045, 31.2365, 12.0), (stored.SubmitLatitude, stored.SubmitLongitude, stored.SubmitAccuracyMeters));
    }

    [Fact]
    public async Task Saving_a_draft_again_replaces_its_lines()
    {
        var id = await Rep.SaveDraftAsync(RepId, null, Request(false, (_concor, 10, 0), (_augmentin, 1, 0)));

        await Rep.SaveDraftAsync(RepId, id, Request(false, (_augmentin, 4, 10)));

        var order = await Rep.GetByIdAsync(id);
        Assert.Equal(OrderStatus.Draft, order.Status);
        var line = Assert.Single(order.Lines);
        Assert.Equal((_augmentin.Id, 4, 10m), (line.ProductId, line.Quantity, line.DiscountPercent));
    }

    [Fact]
    public async Task A_submitted_order_can_no_longer_be_replaced()
    {
        var id = await Rep.SaveDraftAsync(RepId, null, Request(true, (_concor, 1, 0)));

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => Rep.SaveDraftAsync(RepId, id, Request(false, (_concor, 2, 0))));
        Assert.Contains("draft", ex.Message);
    }

    [Fact]
    public async Task Nothing_is_saved_when_one_line_is_invalid()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Rep.SaveDraftAsync(RepId, null, Request(true, (_concor, 5, 0), (_augmentin, 1, 150))));

        Assert.Empty(_db.Orders);
        Assert.Empty(_db.OrderLines);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    public async Task Discount_must_be_between_0_and_100_percent(double discount)
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Rep.SaveDraftAsync(RepId, null, Request(false, (_concor, 1, (decimal)discount))));
        Assert.Contains("Discount", ex.Message);
    }

    [Fact]
    public async Task Submitting_an_empty_order_is_refused()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => Rep.SaveDraftAsync(RepId, null, Request(submit: true)));
    }

    [Fact]
    public async Task A_representative_cannot_read_or_change_another_representatives_order()
    {
        var sara = ServiceFor(OtherRepId, Roles.Representative);
        var saraOrder = await sara.SaveDraftAsync(OtherRepId, null, Request(false, (_concor, 1, 0)));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.GetByIdAsync(saraOrder));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.SaveDraftAsync(RepId, saraOrder, Request(false, (_concor, 9, 0))));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.AddLineAsync(saraOrder, new OrderLineSaveRequest { ProductId = _concor.Id, Quantity = 1 }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.SubmitAsync(saraOrder));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.CancelAsync(saraOrder));

        var list = await Rep.GetListAsync(new() { PageNumber = 1, PageSize = 50 }, null, representativeId: OtherRepId, null);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task A_manager_still_sees_every_representatives_order()
    {
        var id = await Rep.SaveDraftAsync(RepId, null, Request(false, (_concor, 1, 0)));
        var manager = ServiceFor(null, Roles.SalesManager);

        Assert.Equal(id, (await manager.GetByIdAsync(id)).Id);
    }

    [Fact]
    public async Task Linked_visit_must_be_the_reps_own_at_the_same_pharmacy()
    {
        var mine = new PharmacyVisit { PharmacyId = _otherPharmacy.Id, RepresentativeId = RepId, CreatedByUserId = "t" };
        var saras = new PharmacyVisit { PharmacyId = _pharmacy.Id, RepresentativeId = OtherRepId, CreatedByUserId = "t" };
        _db.AddRange(mine, saras);
        await _db.SaveChangesAsync();

        var atOtherPharmacy = Request(false, (_concor, 1, 0));
        atOtherPharmacy.PharmacyVisitId = mine.Id;
        await Assert.ThrowsAsync<ValidationFailedException>(() => Rep.SaveDraftAsync(RepId, null, atOtherPharmacy));

        var someoneElses = Request(false, (_concor, 1, 0));
        someoneElses.PharmacyVisitId = saras.Id;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Rep.SaveDraftAsync(RepId, null, someoneElses));
    }

    [Fact]
    public async Task List_shows_line_count_and_rejection_reason()
    {
        var id = await Rep.SaveDraftAsync(RepId, null, Request(true, (_concor, 1, 0), (_augmentin, 1, 0)));
        await ServiceFor(null, Roles.SalesManager).RejectAsync(id, "Over credit limit");

        var item = Assert.Single((await Rep.GetListAsync(new() { PageNumber = 1, PageSize = 50 }, null, RepId, null)).Items);
        Assert.Equal((2, "Over credit limit", OrderStatus.Rejected), (item.LineCount, item.RejectionReason, item.Status));
    }

    public void Dispose() => _db.Dispose();

    private sealed class Caller(int? representativeId, string[] roles) : ICurrentUserService
    {
        public string? UserId => "u";
        public string? UserName => "u";
        public int? RepresentativeId => representativeId;
        public int? TerritoryId => null;
        public bool IsInRole(string role) => roles.Contains(role);
        public bool HasUnrestrictedAccess => roles.Any(r => r is Roles.Admin or Roles.Management or Roles.SalesManager);
    }

    private sealed class NoUsers : IUserDirectoryService
    {
        public Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
