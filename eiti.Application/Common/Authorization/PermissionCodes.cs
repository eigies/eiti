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

    public const string UsersManage = "users.manage";

    public const string SalesPriceOverride = "sales.override_price";

    public const string BanksManage = "banks.manage";
    public const string ChequesManage = "cheques.manage";

    public const string ProductsViewCost = "products.view_cost";

    public const string SalesCancelHistorical = "sales.cancel.historical";

    // Suppliers
    public const string SuppliersManage = "suppliers.manage";

    // Purchases
    public const string PurchasesAccess = "purchases.access";
    public const string PurchasesCreate = "purchases.create";
    public const string PurchasesUpdate = "purchases.update";
    public const string PurchasesPay = "purchases.pay";
    public const string PurchasesCancel = "purchases.cancel";
}
