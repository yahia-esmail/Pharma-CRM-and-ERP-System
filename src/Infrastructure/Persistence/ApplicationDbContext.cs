using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Infrastructure.Identity;

namespace PharmaERP.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options), IAppDbContext
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorClassification> DoctorClassifications => Set<DoctorClassification>();
    public DbSet<DoctorVisit> DoctorVisits => Set<DoctorVisit>();
    public DbSet<DoctorFollowUp> DoctorFollowUps => Set<DoctorFollowUp>();
    public DbSet<Representative> Representatives => Set<Representative>();
    public DbSet<Territory> Territories => Set<Territory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<VisitPlan> VisitPlans => Set<VisitPlan>();
    public DbSet<VisitPlanItem> VisitPlanItems => Set<VisitPlanItem>();
    public DbSet<PerformanceTarget> PerformanceTargets => Set<PerformanceTarget>();
    public DbSet<LocationPing> LocationPings => Set<LocationPing>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Pharmacy> Pharmacies => Set<Pharmacy>();
    public DbSet<PharmacyVisit> PharmacyVisits => Set<PharmacyVisit>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CustodyTransaction> CustodyTransactions => Set<CustodyTransaction>();
    public DbSet<StockReconciliation> StockReconciliations => Set<StockReconciliation>();
    public DbSet<ReturnTransaction> ReturnTransactions => Set<ReturnTransaction>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionAttachment> CollectionAttachments => Set<CollectionAttachment>();
    public DbSet<FinancialReconciliation> FinancialReconciliations => Set<FinancialReconciliation>();
    public DbSet<RemittanceTransaction> RemittanceTransactions => Set<RemittanceTransaction>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<CustomerList> CustomerLists => Set<CustomerList>();
    public DbSet<CustomerListItem> CustomerListItems => Set<CustomerListItem>();
    public DbSet<CustomerTransferLog> CustomerTransferLogs => Set<CustomerTransferLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<CustomerLocationProposal> CustomerLocationProposals => Set<CustomerLocationProposal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global soft-delete filter (spec 5.3 — transactional/master data is never physically deleted).
        builder.Entity<Doctor>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DoctorVisit>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DoctorFollowUp>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Representative>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Territory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DoctorClassification>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<VisitPlan>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<VisitPlanItem>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PerformanceTarget>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Pharmacy>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PharmacyVisit>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OrderLine>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Sale>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProductBatch>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<StockMovement>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CustodyTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<StockReconciliation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ReturnTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Collection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FinancialReconciliation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CollectionAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<RemittanceTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Supplier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseOrder>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseOrderLine>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseReceipt>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseReceiptLine>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SupplierPayment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CustomerList>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CustomerListItem>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CustomerTransferLog>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FileAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
    }
}
