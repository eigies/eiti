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
    SaleFiscalDocument? PrintableInvoice,
    /// <summary>Última ND (regularización de una NC mal emitida), en cualquier estado. Null si no hubo.</summary>
    SaleFiscalDocument? DebitNote = null)
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

        var debitNote = all
            .Where(document => document.Kind == SaleFiscalDocumentKind.DebitNote)
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
            PrintableInvoice: printable,
            DebitNote: debitNote);
    }

    /// <summary>Factura sobre la que se puede operar (descargar el PDF, emitirle una NC).</summary>
    public SaleFiscalDocument? LiveInvoice =>
        Invoice?.Status == SaleInvoicingStatus.Invoiced ? Invoice : null;

    /// <summary>
    /// Recargo de tarjeta cobrado que no entró en la factura vigente: pasa cuando la venta se facturó en espera y
    /// después se cobró en cuotas (se acepta el cobro y la factura queda como salió). Sin factura vigente, 0.
    /// Una factura sin total guardado (anterior a este dato) facturó la mercadería.
    /// </summary>
    public decimal UninvoicedCardSurcharge(Sale sale)
    {
        if (LiveInvoice is null)
        {
            return 0m;
        }

        var invoiced = LiveInvoice.RequestedTotal ?? sale.TotalAmount;
        return Math.Max(0m, decimal.Round(sale.TotalAmount + sale.CardSurchargeTotal - invoiced, 2, MidpointRounding.AwayFromZero));
    }
}
