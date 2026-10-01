using eiti.Application.Common;
using eiti.Domain.Branches;

namespace eiti.Application.Features.Branches.Commands.SetBranchPointOfSale;

public static class SetBranchPointOfSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Branches.PointOfSale.NotFound",
        "The requested branch was not found.");

    public static readonly Error OutOfRange = Error.Validation(
        "Branches.PointOfSale.OutOfRange",
        $"El punto de venta tiene que estar entre 1 y {FiscalPointOfSale.MaxNumber}.");

    public static Error InUse(int number) => Error.Conflict(
        "Branches.PointOfSale.InUse",
        $"El punto de venta {number} ya lo usa otra sucursal: cada sucursal factura en el suyo.");

    public static Error NotRegistered(string? reason) => Error.Conflict(
        "Branches.PointOfSale.NotRegistered",
        $"No se pudo habilitar el punto de venta en el servicio de facturación: {reason ?? "sin detalle"}");
}
