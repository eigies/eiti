namespace eiti.Application.Common.Authorization;

public static class PermissionCatalog
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PermissionCodes.SalesAccess,
        PermissionCodes.SalesCreate,
        PermissionCodes.SalesUpdate,
        PermissionCodes.SalesDelete,
        PermissionCodes.SalesPay,
        PermissionCodes.CashAccess,
        PermissionCodes.CashOpen,
        PermissionCodes.CashClose,
        PermissionCodes.CashWithdraw,
        PermissionCodes.CashDrawerManage,
        PermissionCodes.CashDrawerViewAll,
        PermissionCodes.CashHistoryExport,
        PermissionCodes.UsersManage,
        PermissionCodes.SalesPriceOverride,
        PermissionCodes.BanksManage,
        PermissionCodes.ChequesManage,
        PermissionCodes.CashDrawerAssign,
        PermissionCodes.ProductsViewCost,
        PermissionCodes.SalesCancelHistorical,
        PermissionCodes.SuppliersManage,
        PermissionCodes.PurchasesAccess,
        PermissionCodes.PurchasesCreate,
        PermissionCodes.PurchasesUpdate,
        PermissionCodes.PurchasesPay,
        PermissionCodes.PurchasesCancel,
        PermissionCodes.ProductsCostPriceAlert,
    };

    public static bool IsValid(string permissionCode) =>
        All.Contains(permissionCode.Trim().ToLowerInvariant());
}
