namespace eiti.Domain.Sales;

/// <summary>
/// Qué comprobante representa la fila. Discrimina factura de nota de crédito sin duplicar
/// columnas: una venta tiene a lo sumo una de cada tipo (índice único por venta + tipo).
/// Arranca en 1: el 0 es el centinela de EF Core.
/// </summary>
public enum SaleFiscalDocumentKind
{
    Invoice = 1,
    CreditNote = 2
}
