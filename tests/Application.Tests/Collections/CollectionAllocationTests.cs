using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Collections;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Collections;

public class CollectionAllocationTests : IDisposable
{
    private const int RepId = 7;
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly PharmacyBalanceCalculator _balances;
    private readonly CollectionService _collections;
    private Pharmacy _pharmacy = null!, _other = null!;
    private Sale _july = null!, _august = null!, _september = null!, _foreign = null!;

    public CollectionAllocationTests()
    {
        _balances = new PharmacyBalanceCalculator(_db);
        _collections = new CollectionService(_db, null!, new NotificationService(_db, TestCalendar.Cairo()), new NoUsers(), _balances);
        Seed();
    }

    private void Seed()
    {
        _pharmacy = new Pharmacy { Name = "Tahrir Pharmacy", CreatedByUserId = "t" };
        _other = new Pharmacy { Name = "Garden City Pharmacy", CreatedByUserId = "t" };
        _db.AddRange(new Representative { Id = RepId, EmployeeCode = "R7", FullName = "Yahia", CreatedByUserId = "t" }, _pharmacy, _other);
        _db.SaveChanges();
        var orderId = 1;
        Sale SaleOf(Pharmacy p, int month, decimal total) => new()
        {
            OrderId = orderId++, PharmacyId = p.Id, RepresentativeId = RepId, TotalAmount = total,
            SaleDateUtc = new DateTime(2026, month, 1, 9, 0, 0, DateTimeKind.Utc), CreatedByUserId = "t"
        };
        _july = SaleOf(_pharmacy, 7, 1000);
        _august = SaleOf(_pharmacy, 8, 2000);
        _september = SaleOf(_pharmacy, 9, 3000);
        _foreign = SaleOf(_other, 9, 500);
        _db.AddRange(_july, _august, _september, _foreign);
        _db.SaveChanges();
    }

    private Task<int> CollectAsync(decimal amount, params (Sale Sale, decimal Amount)[] split) =>
        _collections.RecordCollectionAsync(RepId, new CollectionSaveRequest
        {
            PharmacyId = _pharmacy.Id,
            Amount = amount,
            PaymentMethod = PaymentMethod.Cheque,
            ReferenceNumber = "CHQ-1",
            Allocations = split.Select(s => new CollectionAllocationRequest { SaleId = s.Sale.Id, Amount = s.Amount }).ToList()
        });

    private async Task<(decimal July, decimal August, decimal September)> OpenAsync()
    {
        var b = await _balances.GetAsync(_pharmacy.Id);
        return (b.Sale(_july.Id)!.Open, b.Sale(_august.Id)!.Open, b.Sale(_september.Id)!.Open);
    }

    [Fact]
    public async Task Unallocated_collections_settle_the_oldest_invoices_first()
    {
        await CollectAsync(1500);

        Assert.Equal((0m, 1500m, 3000m), await OpenAsync());
        Assert.Equal(4500, (await _balances.GetAsync(_pharmacy.Id)).Outstanding);
    }

    [Fact]
    public async Task One_cheque_split_across_invoices_settles_exactly_those()
    {
        await CollectAsync(2500, (_september, 2000), (_august, 500));

        Assert.Equal((1000m, 1500m, 1000m), await OpenAsync());
    }

    [Fact]
    public async Task The_unsplit_rest_of_a_collection_goes_oldest_first()
    {
        await CollectAsync(3500, (_september, 3000));   // 500 left over

        Assert.Equal((500m, 2000m, 0m), await OpenAsync());
    }

    [Fact]
    public async Task Ledger_ages_only_what_is_still_unpaid()
    {
        await CollectAsync(3000, (_july, 1000), (_august, 2000));
        var pharmacies = new PharmacyService(_db, null!, null!, TestCalendar.Cairo(), _balances);

        var ledger = await pharmacies.GetLedgerAsync(_pharmacy.Id);

        Assert.Equal((6000m, 3000m, 3000m), (ledger.TotalSales, ledger.TotalCollected, ledger.OutstandingBalance));
        Assert.Equal(3000, ledger.Aging0To30 + ledger.Aging31To60 + ledger.Aging60Plus);
        Assert.Equal(0, ledger.Lines.Single(l => l.SaleId == _july.Id).Open);
        Assert.Equal(0, ledger.Lines.Single(l => l.SaleId == _july.Id).DaysOverdue);   // paid invoices aren't overdue
    }

    [Fact]
    public async Task Another_pharmacys_invoice_is_refused()
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => CollectAsync(100, (_foreign, 100)));
        Assert.Contains("doesn't belong", ex.Message);
    }

    [Fact]
    public async Task Allocating_more_than_an_invoice_still_owes_is_refused()
    {
        await CollectAsync(800, (_july, 800));

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => CollectAsync(500, (_july, 500)));
        Assert.Contains("200", ex.Message);
    }

    [Fact]
    public async Task Split_cannot_exceed_the_amount_collected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => CollectAsync(1000, (_august, 700), (_september, 700)));
        Assert.Empty(_db.Collections);
    }

    [Fact]
    public async Task Legacy_single_invoice_overpayment_settles_it_and_the_rest_goes_oldest_first()
    {
        await _collections.RecordCollectionAsync(RepId, new CollectionSaveRequest
        {
            PharmacyId = _pharmacy.Id, SaleId = _september.Id, Amount = 3500, PaymentMethod = PaymentMethod.Cash
        });

        Assert.Equal((500m, 2000m, 0m), await OpenAsync());
    }

    [Fact]
    public async Task A_collection_dated_in_the_future_is_refused()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => _collections.RecordCollectionAsync(RepId, new CollectionSaveRequest
        {
            PharmacyId = _pharmacy.Id, Amount = 100, PaymentMethod = PaymentMethod.Cash, CollectionDateUtc = DateTime.UtcNow.AddHours(2)
        }));
    }

    [Fact]
    public async Task Proof_photos_only_on_the_reps_own_collection_and_at_most_four()
    {
        var id = await CollectAsync(100);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _collections.AddAttachmentAsync(id, "u", "a.jpg", "image/jpeg", 10,
            new MemoryStream([1]), ownerRepresentativeId: RepId + 1));

        for (var i = 0; i < 4; i++)
            _db.CollectionAttachments.Add(new CollectionAttachment
            {
                CollectionId = id, FileName = $"{i}.jpg", ContentType = "image/jpeg", RelativePath = "x", UploadedByUserId = "u"
            });
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationFailedException>(() => _collections.AddAttachmentAsync(id, "u", "5.jpg", "image/jpeg", 10,
            new MemoryStream([1]), ownerRepresentativeId: RepId));
    }

    public void Dispose() => _db.Dispose();

    private sealed class NoUsers : IUserDirectoryService
    {
        public Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
