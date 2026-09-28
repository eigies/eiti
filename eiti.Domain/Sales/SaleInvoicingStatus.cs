namespace eiti.Domain.Sales;

/// <summary>
/// Estado de la facturación electrónica de una venta.
/// Arranca en 1 a propósito: el 0 es el centinela de EF Core para "sin setear".
/// </summary>
public enum SaleInvoicingStatus
{
    NotInvoiced = 1,
    InProgress = 2,
    Invoiced = 3,
    Rejected = 4,
    /// <summary>
    /// Factura anulada por una nota de crédito autorizada. Es un ESTADO GUARDADO, no algo que
    /// se deduzca recorriendo las NC: así "la factura vigente" es un filtro por estado y no una
    /// regla que cada consumidor tenga que acordarse de aplicar.
    /// </summary>
    Voided = 5
}
