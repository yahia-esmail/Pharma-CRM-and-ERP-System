using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Application.Expenses;
using PharmaERP.Application.Files;
using PharmaERP.Application.Notifications;
using PharmaERP.Application.Returns;
using PharmaERP.Domain.Entities;
using PharmaERP.Domain.Enums;
using PharmaERP.Infrastructure.Persistence;

namespace PharmaERP.Application.Tests.Expenses;

public class ExpenseCreateTests : IDisposable
{
    private const string Rep = "test-user";   // TestDb stamps CreatedByUserId with this
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 2);

    private readonly ApplicationDbContext _db = TestDb.Create(Rep);
    private readonly ExpenseService _expenses;
    private Territory _territory = null!;

    public ExpenseCreateTests()
    {
        var calendar = TestCalendar.Cairo(new FakeTimeProvider(Now));
        _expenses = new ExpenseService(_db, new FileAttachmentService(_db, null!), calendar,
            new NotificationService(_db, calendar), new NoUsers());
        _territory = new Territory { Name = "Cairo North", CreatedByUserId = "t" };
        _db.Territories.Add(_territory);
        _db.SaveChanges();
    }

    private async Task<int> UploadedReceiptAsync(string uploadedBy = Rep, string entityType = "Expense")
    {
        var file = new FileAttachment
        {
            EntityType = entityType, FileName = "receipt.jpg", ContentType = "image/jpeg", RelativePath = "x",
            UploadedByUserId = uploadedBy, UploadedAtUtc = Now.UtcDateTime
        };
        _db.FileAttachments.Add(file);
        await _db.SaveChangesAsync();
        return file.Id;
    }

    private ExpenseSaveRequest Request(bool submit, params int[] attachments) => new()
    {
        Type = ExpenseType.ClientEntertainment,
        Amount = 450,
        ExpenseDate = Today,
        TerritoryId = _territory.Id,
        Description = "  Lunch with Dr. Karim  ",
        AttachmentIds = attachments.ToList(),
        Submit = submit
    };

    [Fact]
    public async Task Expense_with_receipt_is_created_and_submitted_in_one_request()
    {
        var receipt = await UploadedReceiptAsync();

        var id = await _expenses.CreateAsync(Rep, Request(submit: true, receipt));

        var expense = await _db.Expenses.SingleAsync();
        Assert.Equal((id, ExpenseStatus.Submitted, ExpenseType.ClientEntertainment, "Lunch with Dr. Karim"),
            (expense.Id, expense.Status, expense.Type, expense.Description));
        Assert.Equal(id, (await _db.FileAttachments.SingleAsync()).EntityId);
    }

    [Fact]
    public async Task Without_submit_it_stays_a_draft()
    {
        await _expenses.CreateAsync(Rep, Request(submit: false));
        Assert.Equal(ExpenseStatus.Draft, (await _db.Expenses.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_bad_receipt_fails_before_anything_is_saved()
    {
        var someoneElses = await UploadedReceiptAsync(uploadedBy: "another-user");

        await Assert.ThrowsAsync<ValidationFailedException>(() => _expenses.CreateAsync(Rep, Request(true, someoneElses)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _expenses.CreateAsync(Rep, Request(true, 999)));

        Assert.Empty(_db.Expenses);   // a resend can't create a duplicate
    }

    [Fact]
    public async Task Future_dates_are_refused_in_the_business_time_zone()
    {
        var request = Request(false);
        request.ExpenseDate = Today.AddDays(1);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _expenses.CreateAsync(Rep, request));
    }

    [Fact]
    public async Task A_return_batch_must_belong_to_the_product()
    {
        var concor = new Product { Sku = "C", Name = "Concor", UnitOfMeasure = "Box", CreatedByUserId = "t" };
        var augmentin = new Product { Sku = "A", Name = "Augmentin", UnitOfMeasure = "Box", CreatedByUserId = "t" };
        var pharmacy = new Pharmacy { Name = "Tahrir", CreatedByUserId = "t" };
        _db.AddRange(concor, augmentin, pharmacy);
        await _db.SaveChangesAsync();
        var augmentinBatch = new ProductBatch { ProductId = augmentin.Id, BatchNumber = "AUG-1", ExpiryDate = Today.AddYears(1), CreatedByUserId = "t" };
        _db.Add(augmentinBatch);
        await _db.SaveChangesAsync();
        var returns = new ReturnService(_db, null!, null!);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => returns.RequestAsync(7, Rep, new ReturnRequestSaveRequest
        {
            FlowType = ReturnFlowType.CustomerToRepresentative, PharmacyId = pharmacy.Id,
            ProductId = concor.Id, ProductBatchId = augmentinBatch.Id, Quantity = 2, Reason = ReturnReason.Damaged
        }));
        Assert.Contains("batch", ex.Message);
    }

    public void Dispose() => _db.Dispose();

    private sealed class NoUsers : IUserDirectoryService
    {
        public Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
