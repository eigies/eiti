namespace eiti.Domain.Sales;

/// <summary>
/// Lectura única del estado de facturación de una venta a partir de sus comprobantes.
///
/// Existe para que la regla se decida en UN solo lugar: el listado, el detalle, la descarga del
/// PDF y el servicio de emisión proyectan todos con <see cref="From"/>. Antes cada uno elegía por
/// su cuenta qué comprobante mirar, y eran cuatro oportunidades de elegir mal.
/// </summary>
public sealed record SaleInvoicingView(
    SaleInvoicingStatus Status,
    SaleFiscalDocument? Invoice,
    SaleFiscalDocument? CreditNote,
    bool CanInvoice,
    /// <summary>
    /// Factura que llegó a tener comprobante emitido, esté vigente o anulada. Es la que se puede
    /// imprimir: una factura anulada sigue siendo un documento legal que existió y hay que poder
    /// reimprimir. Null si nunca se autorizó ninguna.
    /// </summary>
    SaleFiscalDocument? PrintableInvoice)
{
    public static readonly SaleInvoicingView Empty =
        new(SaleInvoicingStatus.NotInvoiced, null, null, CanInvoice: true, PrintableInvoice: null);

    public static SaleInvoicingView From(IEnumerable<SaleFiscalDocument> documents)
    {
        var all = documents as IReadOnlyCollection<SaleFiscalDocument> ?? documents.ToList();
        if (all.Count == 0)
        {
            return Empty;
        }

        var invoices = all.Where(document => document.Kind == SaleFiscalDocumentKind.Invoice).ToList();

        // La factura vigente es un filtro por estado: la anulación quedó guardada como Voided
        // cuando se autorizó su nota de crédito.
        var live = invoices
            .Where(document => document.Status == SaleInvoicingStatus.Invoiced)
            .OrderByDescending(document => document.Sequence)
            .FirstOrDefault();
        var inProgress = invoices.Any(document => document.Status == SaleInvoicingStatus.InProgress);

        // Si no hay vigente se muestra el último intento, para que un rechazo quede visible con su
        // motivo en vez de desaparecer de la pantalla.
        var invoice = live ?? invoices
            .OrderByDescending(document => document.Sequence)
            .FirstOrDefault();

        var creditNote = all
            .Where(document => document.Kind == SaleFiscalDocumentKind.CreditNote)
            .OrderByDescending(document => document.Sequence)
            .FirstOrDefault();

        // Vigente o anulada: las dos tuvieron CAE, las dos tienen PDF.
        var printable = invoices
            .Where(document => document.Status is SaleInvoicingStatus.Invoiced or SaleInvoicingStatus.Voided)
            .OrderByDescending(document => document.Sequence)
            .FirstOrDefault();

        return new SaleInvoicingView(
            invoice?.Status ?? SaleInvoicingStatus.NotInvoiced,
            invoice,
            creditNote,
            CanInvoice: live is null && !inProgress,
            PrintableInvoice: printable);
    }

    /// <summary>Factura sobre la que se puede operar (descargar el PDF, emitirle una NC).</summary>
    public SaleFiscalDocument? LiveInvoice =>
        Invoice?.Status == SaleInvoicingStatus.Invoiced ? Invoice : null;
}
