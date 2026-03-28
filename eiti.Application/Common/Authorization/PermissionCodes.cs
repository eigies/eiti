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
    public const string CashHistoryExport = "cash.history.export";

    public const string UsersManage = "users.manage";

    public const string SalesPriceOverride = "sales.override_price";

    public const string BanksManage = "banks.manage";
    public const string ChequesManage = "cheques.manage";
}
