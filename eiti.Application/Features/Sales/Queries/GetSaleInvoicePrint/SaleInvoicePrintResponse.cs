namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;

/// <summary>
/// Todo lo que el PDF necesita, ya resuelto: el front solo dibuja.
/// Comprobante, emisor, receptor, importes y asociado vienen del servicio fiscal (lo que avala el
/// CAE). Ítems, ajustes, domicilio y condición de venta vienen de la venta en EITI.
/// Los precios de los ítems van CON IVA incluido, como se vendieron; la vista de la Factura A
/// los muestra netos.
/// </summary>
public sealed record SaleInvoicePrintResponse(
    string Kind,
    string Letter,
    int TypeCode,
    string Title,
    int PointOfSale,
    long Number,
    DateOnly Date,
    string AuthorizationCode,
    DateOnly AuthorizationExpiry,
    string QrUrl,
    bool IsVoided,
    SaleInvoicePrintIssuer Issuer,
    SaleInvoicePrintReceiver Receiver,
    SaleInvoicePrintAssociatedDocument? AssociatedDocument,
    string SaleCode,
    string SaleCondition,
    decimal VatRate,
    IReadOnlyList<SaleInvoicePrintItem> Items,
    SaleInvoicePrintAdjustments Adjustments,
    SaleInvoicePrintAmounts Amounts,
    // Observaciones de la venta (no fiscales): se leen al imprimir, así una corrección sale en la reimpresión.
    string? Notes = null);

public sealed record SaleInvoicePrintIssuer(
    string LegalName,
    string Cuit,
    string VatCondition,
    string? Iibb,
    DateOnly ActivityStartDate,
    string? CommercialAddress,
    string? BranchName);

public sealed record SaleInvoicePrintReceiver(
    string Name,
    string VatCondition,
    string? Identification);

public sealed record SaleInvoicePrintAssociatedDocument(
    string Label,
    int PointOfSale,
    long Number,
    DateOnly Date);

public sealed record SaleInvoicePrintItem(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal Total);

/// <summary>Lo que lleva de la suma de ítems al total facturado.</summary>
/// <param name="CardSurcharge">Recargo de la tarjeta en cuotas que incluye el comprobante autorizado (0 si no lo incluye).</param>
/// <param name="CardSurchargeLabel">Texto del renglón, p. ej. "Recargo financiero tarjeta (Santander, 3 cuotas, 11,06%)"; null sin recargo.</param>
public sealed record SaleInvoicePrintAdjustments(
    decimal ItemsSubtotal,
    decimal NoDeliverySurcharge,
    decimal GeneralDiscountPercent,
    decimal? AgreedPrice,
    decimal CardSurcharge = 0m,
    string? CardSurchargeLabel = null);

public sealed record SaleInvoicePrintAmounts(
    decimal Net,
    IReadOnlyList<SaleInvoicePrintVat> Vat,
    decimal Exempt,
    decimal Total);

public sealed record SaleInvoicePrintVat(decimal Rate, decimal Base, decimal Amount);
