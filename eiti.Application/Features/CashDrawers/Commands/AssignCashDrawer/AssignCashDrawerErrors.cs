using eiti.Application.Common;

namespace eiti.Application.Features.CashDrawers.Commands.AssignCashDrawer;

public static class AssignCashDrawerErrors
{
    public static readonly Error DrawerNotFound = Error.NotFound(
        "CashDrawers.Assign.DrawerNotFound", "La caja no existe.");

    public static readonly Error UserNotFound = Error.NotFound(
        "CashDrawers.Assign.UserNotFound", "El usuario no existe en esta empresa.");
}
