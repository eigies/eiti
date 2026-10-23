using eiti.Domain.Branches;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Se factura en el punto de venta de la sucursal que vende (1:1), sin elegir otro. Una sucursal sin
/// punto de venta no puede facturar: se avisa antes de pedir nada, con lo que hay que hacer.
/// </summary>
public static class SaleInvoicingBranchRules
{
    /// <summary>Null si la sucursal puede facturar; si no, el motivo para mostrarle al usuario.</summary>
    public static string? Validate(Branch? branch) =>
        branch?.FiscalPointOfSale is not null
            ? null
            : $"La sucursal {branch?.Name ?? "de la venta"} no tiene punto de venta de ARCA. Asignale uno en Facturación electrónica para poder facturar.";
}
