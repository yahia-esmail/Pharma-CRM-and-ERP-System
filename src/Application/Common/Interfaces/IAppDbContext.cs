using Microsoft.EntityFrameworkCore;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Application.Common.Interfaces;

/// <summary>
/// Everything Application-layer services need from the persistence layer, without a direct
/// dependency on Infrastructure (spec 5.2 — Domain/Application stay implementation-agnostic).
/// </summary>
public interface IAppDbContext
{
    DbSet<Doctor> Doctors { get; }
    DbSet<DoctorClassification> DoctorClassifications { get; }
    DbSet<DoctorVisit> DoctorVisits { get; }
    DbSet<DoctorFollowUp> DoctorFollowUps { get; }
    DbSet<Representative> Representatives { get; }
    DbSet<Territory> Territories { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<VisitPlan> VisitPlans { get; }
    DbSet<VisitPlanItem> VisitPlanItems { get; }
    DbSet<PerformanceTarget> PerformanceTargets { get; }
    DbSet<LocationPing> LocationPings { get; }
    DbSet<Product> Products { get; }
    DbSet<Pharmacy> Pharmacies { get; }
    DbSet<PharmacyVisit> PharmacyVisits { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderLine> OrderLines { get; }
    DbSet<Sale> Sales { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<ProductBatch> ProductBatches { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<CustodyTransaction> CustodyTransactions { get; }
    DbSet<StockReconciliation> StockReconciliations { get; }
    DbSet<ReturnTransaction> ReturnTransactions { get; }
    DbSet<Collection> Collections { get; }
    DbSet<CollectionAttachment> CollectionAttachments { get; }
    DbSet<FinancialReconciliation> FinancialReconciliations { get; }
    DbSet<RemittanceTransaction> RemittanceTransactions { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<PurchaseReceipt> PurchaseReceipts { get; }
    DbSet<PurchaseReceiptLine> PurchaseReceiptLines { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<CustomerList> CustomerLists { get; }
    DbSet<CustomerListItem> CustomerListItems { get; }
    DbSet<CustomerTransferLog> CustomerTransferLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<FileAttachment> FileAttachments { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<CustomerLocationProposal> CustomerLocationProposals { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
