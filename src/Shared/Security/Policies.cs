namespace PharmaERP.Shared.Security;

/// <summary>Policy-based authorization names (spec 4.9, 5.4) — module + action granularity, not just role checks.</summary>
public static class Policies
{
    public const string DoctorsView = "Doctors.View";
    public const string DoctorsEdit = "Doctors.Edit";
    public const string DoctorsFull = "Doctors.Full";

    public const string RepresentativesView = "Representatives.View";
    public const string RepresentativesEdit = "Representatives.Edit";
    public const string RepresentativesFull = "Representatives.Full";

    public const string UserManagement = "UserManagement.Full";
    public const string AuditTrailView = "AuditTrail.View";

    public const string VisitPlansView = "VisitPlans.View";
    public const string VisitPlansEdit = "VisitPlans.Edit";
    public const string VisitPlansApprove = "VisitPlans.Approve";

    public const string PerformanceView = "Performance.View";
    public const string PerformanceManageTargets = "Performance.ManageTargets";

    public const string LocationView = "Location.View";

    public const string ProductsView = "Products.View";
    public const string ProductsEdit = "Products.Edit";

    public const string PharmaciesView = "Pharmacies.View";
    public const string PharmaciesEdit = "Pharmacies.Edit";
    public const string PharmaciesFull = "Pharmacies.Full";

    public const string OrdersView = "Orders.View";
    public const string OrdersEdit = "Orders.Edit";
    public const string OrdersApprove = "Orders.Approve";

    public const string SalesView = "Sales.View";

    public const string WarehousesView = "Warehouses.View";
    public const string WarehousesEdit = "Warehouses.Edit";

    public const string CustodyView = "Custody.View";
    public const string CustodyManage = "Custody.Manage";
    public const string CustodyReturn = "Custody.Return";

    public const string CollectionsView = "Collections.View";
    public const string CollectionsCreate = "Collections.Create";
    public const string RemittanceConfirm = "Remittance.Confirm";
    public const string ReconciliationRequest = "Reconciliation.Request";
    public const string ReconciliationApprove = "Reconciliation.Approve";

    public const string SuppliersView = "Suppliers.View";
    public const string SuppliersEdit = "Suppliers.Edit";

    public const string PurchaseOrdersView = "PurchaseOrders.View";
    public const string PurchaseOrdersEdit = "PurchaseOrders.Edit";

    public const string DashboardView = "Dashboard.View";
    public const string TraceView = "Trace.View";

    public const string ListsView = "Lists.View";
    public const string ListsEdit = "Lists.Edit";
    public const string ListsFull = "Lists.Full";

    public const string ExpensesView = "Expenses.View";
    public const string ExpensesCreate = "Expenses.Create";
    public const string ExpensesApprove = "Expenses.Approve";
    public const string ExpensesReimburse = "Expenses.Reimburse";
}
