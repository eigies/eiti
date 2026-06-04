namespace eiti.Application.Common.Authorization;

public static class PermissionCodes
{
    public const string SalesAccess = "sales.access";
    public const string SalesCreate = "sales.create";
    public const string SalesUpdate = "sales.update";
    public const string SalesDelete = "sales.delete";
    public const string SalesPay = "sales.pay";

    public const string CashAccess = "cash.access";
    public const string CashOpen = "cash.open";
    public const string CashClose = "cash.close";
    public const string CashWithdraw = "cash.withdraw";
    public const string CashDrawerManage = "cash.drawer.manage";
    public const string CashDrawerAssign = "cash.drawer.assign";
    public const string CashDrawerViewAll = "cash.drawer.view_all";
    public const string CashHistoryExport = "cash.history.export";

    public const string BranchesViewAll = "branches.view_all";

    public const string UsersManage = "users.manage";

    public const string SalesPriceOverride = "sales.override_price";

    public const string BanksManage = "banks.manage";
    public const string ChequesManage = "cheques.manage";

    public const string ProductsViewCost = "products.view_cost";
    public const string ProductsDelete = "products.delete";

    public const string StockManage = "stock.manage";
    public const string StockTransfer = "stock.transfer";

    public const string DashboardViewFinancials = "dashboard.view_financials";

    public const string ReportsAudit = "reports.audit";

    // Reportería comercial / operativa (uno por reporte)
    public const string ReportsSalesModel = "reports.sales.model";
    public const string ReportsSalesComparison = "reports.sales.comparison";
    public const string ReportsSalesTransport = "reports.sales.transport";
    public const string ReportsSalesChannel = "reports.sales.channel";
    public const string ReportsSalesChannelBrand = "reports.sales.channel_brand";
    public const string ReportsSalesBrand = "reports.sales.brand";
    public const string ReportsSalesRanking = "reports.sales.ranking";
    public const string ReportsDebtors = "reports.debtors";
    public const string ReportsCash = "reports.cash";

    public const string SalesCancelHistorical = "sales.cancel.historical";

    // Suppliers
    public const string SuppliersManage = "suppliers.manage";

    // Purchases
    public const string PurchasesAccess = "purchases.access";
    public const string PurchasesCreate = "purchases.create";
    public const string PurchasesUpdate = "purchases.update";
    public const string PurchasesPay = "purchases.pay";
    public const string PurchasesCancel = "purchases.cancel";

    public const string ProductsCostPriceAlert = "products.cost_price_alert";
}
