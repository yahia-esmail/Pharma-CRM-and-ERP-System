using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Expenses;
using PharmaERP.Application.Files;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Orders;
using PharmaERP.Application.Pharmacies;
using PharmaERP.Application.Returns;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;
using PharmaERP.Shared.Security;

namespace PharmaERP.Application.Tests.Notifications;

public class RepNotificationTests : IDisposable
{
    private const int RepId = 7;
    private const string RepUser = "rep-user";
    private readonly ApplicationDbContext _db = TestDb.Create(RepUser);
    private readonly RecordingPush _push = new();
    private readonly NotificationService _notifications;

    public RepNotificationTests()
    {
        _notifications = new NotificationService(_db, TestCalendar.Cairo(), _push);
        _db.Add(new Representative { Id = RepId, EmployeeCode = "R7", FullName = "Yahia", ApplicationUserId = RepUser, CreatedByUserId = "t" });
        _db.SaveChanges();
    }

    private async Task<IReadOnlyList<Notification>> InboxAsync() =>
        await _db.Notifications.Where(n => n.RecipientUserId == RepUser).ToListAsync();

    [Fact]
    public async Task Rep_hears_when_their_order_is_rejected_with_the_reason_and_it_is_pushed()
    {
        var pharmacy = new Pharmacy { Name = "Tahrir", CreatedByUserId = "t" };
        _db.Add(pharmacy);
        await _db.SaveChangesAsync();
        var order = new Order { PharmacyId = pharmacy.Id, RepresentativeId = RepId, Status = OrderStatus.Submitted, CreatedByUserId = "t" };
        _db.Add(order);
        await _db.SaveChangesAsync();
        var orders = new OrderService(_db, null!, _notifications, new NoUsers(), new Manager(), new PharmacyBalanceCalculator(_db));

        await orders.RejectAsync(order.Id, "Over credit limit");

        var n = Assert.Single(await InboxAsync());
        Assert.Equal((NotificationTypes.OrderRejected, nameof(Order), (int?)order.Id), (n.Type, n.RelatedEntityType, n.RelatedEntityId));
        Assert.Contains("Over credit limit", n.Message);
        var pushed = Assert.Single(_push.Sent);
        Assert.Equal((RepUser, n.Id), (pushed.RecipientUserId, pushed.NotificationId));
    }

    [Fact]
    public async Task Rep_hears_when_their_expense_is_approved()
    {
        var territory = new Territory { Name = "Cairo North", CreatedByUserId = "t" };
        _db.Add(territory);
        await _db.SaveChangesAsync();
        var expenses = new ExpenseService(_db, new FileAttachmentService(_db, null!), TestCalendar.Cairo(new FakeTimeProvider(DateTimeOffset.UtcNow)),
            _notifications, new NoUsers());
        var id = await expenses.CreateAsync(RepUser, new ExpenseSaveRequest
        {
            Type = ExpenseType.Fuel, Amount = 300, ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TerritoryId = territory.Id, Submit = true
        });

        await expenses.ApproveAsync(id, "manager");

        Assert.Equal(NotificationTypes.ExpenseApproved, Assert.Single(await InboxAsync()).Type);
    }

    [Fact]
    public async Task Rep_hears_when_their_return_is_rejected()
    {
        var product = new Product { Sku = "C", Name = "Concor", UnitOfMeasure = "Box", CreatedByUserId = "t" };
        var pharmacy = new Pharmacy { Name = "Tahrir", CreatedByUserId = "t" };
        _db.AddRange(product, pharmacy);
        await _db.SaveChangesAsync();
        var returns = new ReturnService(_db, null!, _notifications);
        var id = await returns.RequestAsync(RepId, RepUser, new ReturnRequestSaveRequest
        {
            FlowType = ReturnFlowType.CustomerToRepresentative, PharmacyId = pharmacy.Id, ProductId = product.Id, Quantity = 2, Reason = ReturnReason.Damaged
        });

        await returns.RejectAsync(id, "No damage visible");

        var n = Assert.Single(await InboxAsync());
        Assert.Equal(NotificationTypes.ReturnRejected, n.Type);
        Assert.Contains("No damage visible", n.Message);
    }

    [Fact]
    public async Task Subscribing_the_same_browser_twice_keeps_one_subscription_owned_by_the_latest_user()
    {
        var service = new PushSubscriptionService(_db, TimeProvider.System);
        var request = new PushSubscriptionRequest
        {
            Endpoint = "https://fcm.googleapis.com/fcm/send/abc", Keys = new PushSubscriptionKeys { P256dh = "p1", Auth = "a1" }
        };

        await service.SubscribeAsync("first-user", request);
        request.Keys = new PushSubscriptionKeys { P256dh = "p2", Auth = "a2" };
        await service.SubscribeAsync(RepUser, request);

        var s = Assert.Single(_db.PushSubscriptions);
        Assert.Equal((RepUser, "p2"), (s.UserId, s.P256dh));

        await service.UnsubscribeAsync("first-user", request.Endpoint);   // not theirs any more
        Assert.Single(_db.PushSubscriptions);
        await service.UnsubscribeAsync(RepUser, request.Endpoint);
        Assert.Empty(_db.PushSubscriptions);
    }

    [Fact]
    public async Task Only_https_push_endpoints_with_keys_are_accepted()
    {
        var service = new PushSubscriptionService(_db, TimeProvider.System);
        await Assert.ThrowsAsync<ValidationFailedException>(() => service.SubscribeAsync(RepUser, new PushSubscriptionRequest
        {
            Endpoint = "http://evil.example/push", Keys = new PushSubscriptionKeys { P256dh = "p", Auth = "a" }
        }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => service.SubscribeAsync(RepUser, new PushSubscriptionRequest
        {
            Endpoint = "https://fcm.googleapis.com/fcm/send/x", Keys = new PushSubscriptionKeys()
        }));
    }

    public void Dispose() => _db.Dispose();

    private sealed class RecordingPush : IPushNotifier
    {
        public List<PushMessage> Sent { get; } = [];
        public void Enqueue(PushMessage message) => Sent.Add(message);
    }

    private sealed class NoUsers : IUserDirectoryService
    {
        public Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class Manager : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? UserName => "manager";
        public int? RepresentativeId => null;
        public int? TerritoryId => null;
        public bool IsInRole(string role) => role == Roles.SalesManager;
        public bool HasUnrestrictedAccess => true;
    }
}
