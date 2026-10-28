namespace eiti.Domain.Sales;

/// <summary>
/// Qué comprobante representa la fila. Discrimina factura, nota de crédito y nota de débito sin
/// duplicar columnas (índice único por venta + tipo + intento).
/// Arranca en 1: el 0 es el centinela de EF Core.
/// </summary>
public enum SaleFiscalDocumentKind
{
    Invoice = 1,
    CreditNote = 2,
    /// <summary>
    /// Hoy solo regulariza una NC emitida a un receptor que no era el de la factura: la ND va a ESE
    /// receptor y la neutraliza, para emitir después la NC correcta.
    /// </summary>
    DebitNote = 3
}
