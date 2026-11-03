using eiti.Domain.Sales;

namespace eiti.Application.Features.Reports.Common;

// Tipo de venta de los reportes: Mayorista es Cuenta Corriente, Minorista es venta normal; "all" o vacío no filtra.
public static class ReportSaleType
{
    private static readonly string[] Allowed = ["all", "wholesale", "retail"];

    public static bool IsValid(string? saleType) =>
        string.IsNullOrWhiteSpace(saleType) || Allowed.Contains(saleType.ToLowerInvariant());

    public static bool Matches(Sale sale, string? saleType) =>
        saleType?.ToLowerInvariant() switch
        {
            "wholesale" => sale.IsCuentaCorriente,
            "retail" => !sale.IsCuentaCorriente,
            _ => true
        };
}
