using eiti.Application.Abstractions.Services;
using eiti.Domain.Branches;
using eiti.Domain.Customers;

namespace eiti.Application.Features.FiscalSettings.Common;

/// <summary>
/// Pantalla "Facturación electrónica": datos del emisor (viven en el servicio de facturación) y los
/// puntos de venta de la empresa con la sucursal que factura en cada uno.
/// </summary>
public sealed record FiscalSettingsResponse(
    bool Enabled,
    FiscalIssuerResponse? Issuer,
    // Por qué no se pudieron leer los datos del emisor (ej. el servicio todavía no tiene perfil).
    string? IssuerUnavailableReason,
    IReadOnlyList<FiscalPointOfSaleResponse> PointsOfSale);

public sealed record FiscalIssuerResponse(
    string Cuit,
    string LegalName,
    IvaCondition IvaCondition,
    string? Iibb,
    DateOnly ActivityStartDate,
    string? CommercialAddress,
    bool IsProduction,
    DateTimeOffset? CertificateNotAfter)
{
    public static FiscalIssuerResponse From(FiscalIssuerProfile issuer) => new(issuer.Cuit, issuer.LegalName, issuer.VatCondition, issuer.Iibb,
        issuer.ActivityStartDate, issuer.CommercialAddress, issuer.IsProduction, issuer.CertificateNotAfter);
}

public sealed record FiscalPointOfSaleResponse(Guid Id, int Number, Guid? BranchId, string? BranchName)
{
    public static FiscalPointOfSaleResponse From(FiscalPointOfSale pointOfSale, Branch? branch) =>
        new(pointOfSale.Id.Value, pointOfSale.Number, branch?.Id.Value, branch?.Name);
}
