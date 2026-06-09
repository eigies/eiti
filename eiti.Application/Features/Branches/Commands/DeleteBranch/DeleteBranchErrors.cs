using eiti.Application.Common;

namespace eiti.Application.Features.Branches.Commands.DeleteBranch;

public static class DeleteBranchErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Branches.Delete.NotFound",
        "La sucursal no fue encontrada.");

    public static readonly Error InUse = Error.Conflict(
        "Branches.Delete.InUse",
        "No se puede eliminar la sucursal porque tiene ventas, cajas, stock o usuarios asignados.");
}
