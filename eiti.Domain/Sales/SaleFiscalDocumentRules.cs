namespace eiti.Domain.Sales;

/// <summary>
/// Reglas del lado de la EMISIÓN. Lo que es lectura del estado (qué comprobante mostrar, si se
/// puede facturar) vive en <see cref="SaleInvoicingView"/>, para no tener dos lugares que
/// contesten la misma pregunta.
/// </summary>
public static class SaleFiscalDocumentRules
{
    /// <summary>
    /// Próximo nº de intento para esa venta y tipo. Determinístico: sale de los intentos que ya
    /// existen, nunca de un random. De acá depende la idempotencia contra el servicio fiscal.
    /// </summary>
    public static int NextSequence(IEnumerable<SaleFiscalDocument> documents, SaleFiscalDocumentKind kind)
    {
        var last = documents
            .Where(document => document.Kind == kind)
            .Select(document => (int?)document.Sequence)
            .Max();

        return (last ?? 0) + 1;
    }

    /// <summary>
    /// Intento en curso que se puede re-enviar tal cual. Reintentar con su MISMO RequestId es
    /// seguro: si el servicio ya lo emitió, lo devuelve en vez de emitir otro.
    /// </summary>
    public static SaleFiscalDocument? InFlight(IEnumerable<SaleFiscalDocument> documents, SaleFiscalDocumentKind kind) =>
        documents
            .Where(document => document.Kind == kind && document.Status == SaleInvoicingStatus.InProgress)
            .OrderByDescending(document => document.Sequence)
            .FirstOrDefault();
}
