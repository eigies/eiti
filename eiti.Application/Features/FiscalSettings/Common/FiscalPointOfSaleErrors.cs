using eiti.Application.Common;
using eiti.Domain.Branches;

namespace eiti.Application.Features.FiscalSettings.Common;

public static class FiscalPointOfSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "FiscalPointsOfSale.NotFound",
        "El punto de venta no existe.");

    public static readonly Error BranchNotFound = Error.NotFound(
        "FiscalPointsOfSale.BranchNotFound",
        "La sucursal no existe.");

    public static readonly Error OutOfRange = Error.Validation(
        "FiscalPointsOfSale.OutOfRange",
        $"El punto de venta tiene que estar entre 1 y {FiscalPointOfSale.MaxNumber}.");

    public static Error InUse(int number) => Error.Conflict(
        "FiscalPointsOfSale.InUse",
        $"El punto de venta {number} ya está cargado.");

    public static Error BranchHasAnother(string branchName, int number) => Error.Conflict(
        "FiscalPointsOfSale.BranchHasAnother",
        $"La sucursal {branchName} ya factura con el punto de venta {number}. Sacáselo primero.");

    public static Error AssignedToBranch(string branchName) => Error.Conflict(
        "FiscalPointsOfSale.AssignedToBranch",
        $"El punto de venta lo usa la sucursal {branchName}. Desasignalo antes de borrarlo.");

    public static Error NotRegistered(string? reason) => Error.Conflict(
        "FiscalPointsOfSale.NotRegistered",
        $"No se pudo habilitar el punto de venta en el servicio de facturación: {reason ?? "sin detalle"}");
}
