namespace eiti.Application.Common.Authorization;

public static class RoleCatalog
{
    public static readonly IReadOnlyList<RoleDefinition> All = new[]
    {
        new RoleDefinition(
            SystemRoles.Owner,
            "Owner",
            "Acceso total a ventas, caja y administracion de usuarios.",
            new[]
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
                PermissionCodes.CashHistoryExport,
                PermissionCodes.UsersManage,
                PermissionCodes.SalesPriceOverride,
                PermissionCodes.BanksManage,
                PermissionCodes.ChequesManage
            }),
        new RoleDefinition(
            SystemRoles.Admin,
            "Admin",
            "Administra la operacion comercial y de caja.",
            new[]
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
                PermissionCodes.CashHistoryExport,
                PermissionCodes.UsersManage,
                PermissionCodes.SalesPriceOverride,
                PermissionCodes.BanksManage,
                PermissionCodes.ChequesManage
            }),
        new RoleDefinition(
            SystemRoles.Seller,
            "Vendedor",
            "Opera ventas sin permisos de caja.",
            new[]
            {
                PermissionCodes.SalesAccess,
                PermissionCodes.SalesCreate,
                PermissionCodes.SalesUpdate,
                PermissionCodes.SalesPay
            }),
        new RoleDefinition(
            SystemRoles.Cashier,
            "Cajero",
            "Opera caja sin administracion de usuarios.",
            new[]
            {
                PermissionCodes.CashAccess,
                PermissionCodes.CashOpen,
                PermissionCodes.CashClose,
                PermissionCodes.CashWithdraw,
                PermissionCodes.CashHistoryExport
            })
    };

    public static bool IsValid(string roleCode) =>
        All.Any(role => role.Code.Equals(roleCode, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlySet<string> PermissionsFor(IEnumerable<string> roleCodes) =>
        roleCodes
            .Select(roleCode => roleCode.Trim().ToLowerInvariant())
            .Distinct()
            .Join(
                All,
                code => code,
                role => role.Code,
                (_, role) => role.Permissions)
            .SelectMany(permissions => permissions)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
