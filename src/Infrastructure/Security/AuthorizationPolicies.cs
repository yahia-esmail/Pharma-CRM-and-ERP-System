using Microsoft.AspNetCore.Authorization;
using PharmaERP.Shared.Security;

namespace PharmaERP.Infrastructure.Security;

/// <summary>
/// Module + action level policies (spec 4.9) layered on top of role membership. District Manager gets
/// the same policies as Sales Manager here; the territory *scoping* itself happens in the Application
/// layer services (see ICurrentUserService), not in these role checks.
/// </summary>
public static class AuthorizationPolicies
{
    public static AuthorizationOptions AddPharmaPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Policies.DoctorsView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.DoctorsEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.DoctorsFull, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager));

        options.AddPolicy(Policies.RepresentativesView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));
        options.AddPolicy(Policies.RepresentativesEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager));
        options.AddPolicy(Policies.RepresentativesFull, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager));

        options.AddPolicy(Policies.UserManagement, p => p.RequireRole(Roles.Admin));
        options.AddPolicy(Policies.AuditTrailView, p => p.RequireRole(Roles.Admin, Roles.Management));

        options.AddPolicy(Policies.VisitPlansView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.VisitPlansEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.VisitPlansApprove, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager));

        options.AddPolicy(Policies.PerformanceView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));
        options.AddPolicy(Policies.PerformanceManageTargets, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager));

        options.AddPolicy(Policies.LocationView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));

        // Matches the "Warehouses & Stock" row of the permissions matrix (spec 7) — Products is the
        // slice of that module pulled forward for Order lines ahead of full Inventory in Phase 4.
        options.AddPolicy(Policies.ProductsView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Warehouse, Roles.Procurement));
        options.AddPolicy(Policies.ProductsEdit, p => p.RequireRole(Roles.Admin, Roles.Warehouse));

        options.AddPolicy(Policies.PharmaciesView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));
        options.AddPolicy(Policies.PharmaciesEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.PharmaciesFull, p => p.RequireRole(Roles.Admin, Roles.SalesManager));

        options.AddPolicy(Policies.OrdersView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));
        options.AddPolicy(Policies.OrdersEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        // Approve/Reject is a manager-level gate (addendum 3.4) — a representative can submit but not
        // approve their own order.
        options.AddPolicy(Policies.OrdersApprove, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager));

        options.AddPolicy(Policies.SalesView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));

        options.AddPolicy(Policies.WarehousesView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Warehouse, Roles.Procurement));
        options.AddPolicy(Policies.WarehousesEdit, p => p.RequireRole(Roles.Admin, Roles.Warehouse));

        options.AddPolicy(Policies.CustodyView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Warehouse));
        options.AddPolicy(Policies.CustodyManage, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Warehouse));
        options.AddPolicy(Policies.CustodyReturn, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Warehouse, Roles.Representative));

        options.AddPolicy(Policies.CollectionsView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));
        options.AddPolicy(Policies.CollectionsCreate, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));
        // Remittance requires finance-role confirmation (spec 4.6) before a rep's outstanding balance drops.
        options.AddPolicy(Policies.RemittanceConfirm, p => p.RequireRole(Roles.Admin, Roles.Finance));

        // Financial Reconciliation (addendum 3.6) — Finance performs the cash count; a manager approves
        // it (separation of duties, mirrors the Returns module's request-then-approve split).
        options.AddPolicy(Policies.ReconciliationRequest, p => p.RequireRole(Roles.Admin, Roles.Finance));
        options.AddPolicy(Policies.ReconciliationApprove, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));

        // Matches the "Suppliers & Purchases" row of the permissions matrix (spec 7).
        options.AddPolicy(Policies.SuppliersView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.Warehouse, Roles.Finance, Roles.Procurement));
        options.AddPolicy(Policies.SuppliersEdit, p => p.RequireRole(Roles.Admin, Roles.Procurement));

        options.AddPolicy(Policies.PurchaseOrdersView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.Warehouse, Roles.Finance, Roles.Procurement));
        options.AddPolicy(Policies.PurchaseOrdersEdit, p => p.RequireRole(Roles.Admin, Roles.Procurement));

        // Matches the "Management Dashboard" row of the permissions matrix (spec 7) — Representative has
        // no access there; Warehouse/Finance/Procurement get "relevant KPIs" via their own module screens
        // (Warehouses/Reports, Supplier ledgers, Collections) rather than a filtered copy of this page.
        options.AddPolicy(Policies.DashboardView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));

        // Trace walks data that's individually visible across Warehouses/Custody/Sales/Suppliers — open
        // it to anyone who already has view rights on at least one of those modules.
        options.AddPolicy(Policies.TraceView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Warehouse,
            Roles.Finance, Roles.Procurement));

        // Lists Management (addendum 3.1) — same audience as Representatives (it redistributes customer
        // assignment, a manager-level action), plus Representative gets read-only visibility of their own lists.
        options.AddPolicy(Policies.ListsView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.ListsEdit, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager));
        options.AddPolicy(Policies.ListsFull, p => p.RequireRole(Roles.Admin, Roles.SalesManager));

        // Expense Management (addendum 3.9) — any field-facing role can submit; approval is manager-level
        // (mirrors Orders' submit-vs-approve split); reimbursement is Finance's own step.
        options.AddPolicy(Policies.ExpensesView, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager, Roles.Representative, Roles.Finance));
        options.AddPolicy(Policies.ExpensesCreate, p => p.RequireRole(
            Roles.Admin, Roles.SalesManager, Roles.DistrictManager, Roles.Representative));
        options.AddPolicy(Policies.ExpensesApprove, p => p.RequireRole(
            Roles.Admin, Roles.Management, Roles.SalesManager, Roles.DistrictManager));
        options.AddPolicy(Policies.ExpensesReimburse, p => p.RequireRole(Roles.Admin, Roles.Finance));

        return options;
    }
}
