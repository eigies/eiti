using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Sales;

/// <summary>
/// Comprobante fiscal asociado a una venta. Vive en su propia tabla y es su propio agregado:
/// la venta no sabe nada de facturación, solo ocurre que alguien emitió un comprobante por ella.
///
/// Los datos son un ESPEJO de lo que resolvió el servicio fiscal (eiti-fiscalization), que es
/// el dueño de la verdad. Acá se guardan para poder mostrarlos y para poder asociar después la
/// nota de crédito, sin volver a pedirlos.
/// </summary>
public sealed class SaleFiscalDocument : AggregateRoot<SaleFiscalDocumentId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public SaleId SaleId { get; private set; } = null!;
    public SaleFiscalDocumentKind Kind { get; private set; }
    /// <summary>
    /// Nº de intento de emisión para esta venta y tipo. Arranca en 1 y solo avanza cuando el
    /// intento anterior quedó rechazado. Es lo que hace que <see cref="RequestId"/> sea
    /// determinístico.
    /// </summary>
    public int Sequence { get; private set; }
    public SaleInvoicingStatus Status { get; private set; }
    /// <summary>Id del comprobante en el servicio fiscal. Es la clave con la que llega el callback.</summary>
    public Guid? FiscalDocumentId { get; private set; }
    /// <summary>
    /// El comprobante asociado (CbtesAsoc). En una NC, la FACTURA que anula — no la venta. En una ND,
    /// la NC que neutraliza. Null en una factura.
    /// </summary>
    public SaleFiscalDocumentId? ReversedDocumentId { get; private set; }
    /// <summary>Tipo que resolvió el fisco (ej. "invoiceB"). Necesario para asociar la NC.</summary>
    public string? DocumentType { get; private set; }
    public int? PointOfSale { get; private set; }
    public long? Number { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public DateTime? AuthorizationExpiry { get; private set; }
    public string? QrUrl { get; private set; }
    public string? RejectionReason { get; private set; }
    /// <summary>
    /// Total que se pidió autorizar en una factura (mercadería + recargo de tarjeta cobrado hasta ese momento).
    /// Null en NC/ND y en las facturas pedidas antes de que existiera: esas facturaron la mercadería
    /// (TotalAmount), porque el recargo nunca se facturaba. Con esto se ve el recargo que se cobró después de
    /// facturar y quedó afuera de la factura (<see cref="SaleInvoicingView.UninvoicedCardSurcharge"/>).
    /// </summary>
    public decimal? RequestedTotal { get; private set; }
    public DateTime? IssuedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool IsAuthorized => Status == SaleInvoicingStatus.Invoiced;
    public bool CanRetry => Status == SaleInvoicingStatus.Rejected;

    /// <summary>
    /// Clave de idempotencia que ve el servicio fiscal. DETERMINÍSTICA a propósito: si se pierde
    /// la respuesta y se reintenta, el servicio reconoce el mismo pedido y devuelve el comprobante
    /// que ya emitió en vez de emitir otro. Un CAE no se puede des-emitir.
    /// </summary>
    public string RequestId => $"{SaleId.Value}:{KindCode(Kind)}:{Sequence}";

    private static string KindCode(SaleFiscalDocumentKind kind) => kind switch
    {
        SaleFiscalDocumentKind.Invoice => "inv",
        SaleFiscalDocumentKind.CreditNote => "cn",
        SaleFiscalDocumentKind.DebitNote => "dn",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown fiscal document kind.")
    };

    /// <summary>
    /// Inversa de <see cref="RequestId"/>. Vive al lado del formato para que no se puedan
    /// desincronizar. La usa el callback como respaldo cuando todavía no se registró el id que
    /// asignó el servicio fiscal (por ejemplo si se perdió la respuesta del pedido original).
    /// </summary>
    public static bool TryParseRequestId(
        string? requestId,
        out SaleId saleId,
        out SaleFiscalDocumentKind kind,
        out int sequence)
    {
        saleId = null!;
        kind = SaleFiscalDocumentKind.Invoice;
        sequence = 0;

        var parts = requestId?.Split(':');
        if (parts is not { Length: 3 } ||
            !Guid.TryParse(parts[0], out var parsedSaleId) ||
            !int.TryParse(parts[2], out var parsedSequence) ||
            parsedSequence < 1)
        {
            return false;
        }

        kind = parts[1] switch
        {
            "inv" => SaleFiscalDocumentKind.Invoice,
            "cn" => SaleFiscalDocumentKind.CreditNote,
            "dn" => SaleFiscalDocumentKind.DebitNote,
            _ => (SaleFiscalDocumentKind)0
        };

        if (kind == 0)
        {
            return false;
        }

        saleId = new SaleId(parsedSaleId);
        sequence = parsedSequence;
        return true;
    }

    private SaleFiscalDocument()
    {
    }

    private SaleFiscalDocument(
        SaleFiscalDocumentId id,
        CompanyId companyId,
        SaleId saleId,
        SaleFiscalDocumentKind kind,
        int sequence,
        SaleFiscalDocumentId? reversedDocumentId)
        : base(id)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence starts at 1.");
        }

        CompanyId = companyId;
        SaleId = saleId;
        Kind = kind;
        Sequence = sequence;
        ReversedDocumentId = reversedDocumentId;
        Status = SaleInvoicingStatus.InProgress;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cada intento de emisión es una fila. La secuencia la calcula el llamador a partir de los
    /// intentos previos (ver SaleFiscalDocumentRules.NextSequence): NO es aleatoria, porque de
    /// ella sale la clave de idempotencia.
    /// </summary>
    public static SaleFiscalDocument CreateInvoice(CompanyId companyId, SaleId saleId, int sequence) =>
        new(SaleFiscalDocumentId.New(), companyId, saleId, SaleFiscalDocumentKind.Invoice, sequence, null);

    public static SaleFiscalDocument CreateCreditNote(
        CompanyId companyId,
        SaleId saleId,
        SaleFiscalDocument reversed,
        int sequence)
    {
        if (reversed.Kind != SaleFiscalDocumentKind.Invoice)
        {
            throw new InvalidOperationException("A credit note can only reverse an invoice.");
        }

        if (!reversed.IsAuthorized)
        {
            throw new InvalidOperationException("Only an authorized invoice can be reversed.");
        }

        return new(SaleFiscalDocumentId.New(), companyId, saleId, SaleFiscalDocumentKind.CreditNote, sequence, reversed.Id);
    }

    /// <summary>
    /// ND que neutraliza una NC autorizada (la asocia a ESA NC). Se usa para regularizar una NC que
    /// salió a un receptor que no era el de la factura.
    /// </summary>
    public static SaleFiscalDocument CreateDebitNote(
        CompanyId companyId,
        SaleId saleId,
        SaleFiscalDocument creditNote,
        int sequence)
    {
        if (creditNote.Kind != SaleFiscalDocumentKind.CreditNote)
        {
            throw new InvalidOperationException("A debit note can only offset a credit note.");
        }

        if (!creditNote.IsAuthorized)
        {
            throw new InvalidOperationException("Only an authorized credit note can be offset.");
        }

        return new(SaleFiscalDocumentId.New(), companyId, saleId, SaleFiscalDocumentKind.DebitNote, sequence, creditNote.Id);
    }

    /// <summary>
    /// NC que vuelve a anular una factura cuya NC anterior quedó neutralizada por una ND. La factura
    /// sigue Voided (ya se anuló una vez); lo que se exige es que la NC que la anuló esté neutralizada,
    /// para no dejar dos NC vigentes sobre la misma factura.
    /// </summary>
    public static SaleFiscalDocument CreateCreditNoteReplacing(
        CompanyId companyId,
        SaleId saleId,
        SaleFiscalDocument invoice,
        SaleFiscalDocument neutralizedCreditNote,
        int sequence)
    {
        if (invoice.Kind != SaleFiscalDocumentKind.Invoice || invoice.Status != SaleInvoicingStatus.Voided)
        {
            throw new InvalidOperationException("Only an invoice voided by a credit note can be reversed again.");
        }

        if (neutralizedCreditNote.Kind != SaleFiscalDocumentKind.CreditNote ||
            neutralizedCreditNote.ReversedDocumentId != invoice.Id ||
            neutralizedCreditNote.Status != SaleInvoicingStatus.Voided)
        {
            throw new InvalidOperationException("The credit note of the invoice must be offset by a debit note first.");
        }

        return new(SaleFiscalDocumentId.New(), companyId, saleId, SaleFiscalDocumentKind.CreditNote, sequence, invoice.Id);
    }

    /// <summary>
    /// Fija el total que se pide autorizar. Un intento se re-envía siempre con su mismo RequestId, así que su
    /// total no cambia: volver a fijarlo con el mismo valor no hace nada, con otro es un error de programación.
    /// </summary>
    public void RecordRequestedTotal(decimal total)
    {
        if (Kind != SaleFiscalDocumentKind.Invoice)
        {
            throw new InvalidOperationException("Only an invoice records the total it requested.");
        }

        if (total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "The requested total cannot be negative.");
        }

        var normalized = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
        if (RequestedTotal == normalized)
        {
            return;
        }

        if (RequestedTotal is not null)
        {
            throw new InvalidOperationException("An invoice attempt is always resent with the total it first requested.");
        }

        RequestedTotal = normalized;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Idempotente: el callback del servicio fiscal es at-least-once.</summary>
    public void Authorize(
        Guid fiscalDocumentId,
        string? documentType,
        int? pointOfSale,
        long? number,
        string? authorizationCode,
        DateTime? authorizationExpiry,
        string? qrUrl,
        DateTime occurredAt)
    {
        if (Status == SaleInvoicingStatus.Invoiced && FiscalDocumentId == fiscalDocumentId)
        {
            return;
        }

        FiscalDocumentId = fiscalDocumentId;
        DocumentType = documentType;
        PointOfSale = pointOfSale;
        Number = number;
        AuthorizationCode = authorizationCode;
        AuthorizationExpiry = authorizationExpiry;
        QrUrl = qrUrl;
        RejectionReason = null;
        Status = SaleInvoicingStatus.Invoiced;
        IssuedAt = occurredAt;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Quedó encolado en el servicio: se resuelve por callback.</summary>
    public void Queue(Guid fiscalDocumentId)
    {
        // Una autorización ya confirmada nunca vuelve atrás por un mensaje demorado.
        if (Status == SaleInvoicingStatus.Invoiced)
        {
            return;
        }

        FiscalDocumentId = fiscalDocumentId;
        Status = SaleInvoicingStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// No se pudo confirmar qué pasó: timeout, caída de red, respuesta perdida. El comprobante
    /// PUEDE haberse emitido, así que el documento queda <see cref="SaleInvoicingStatus.InProgress"/>
    /// y NO se rechaza: rechazarlo haría que el reintento use una secuencia nueva y emita un
    /// SEGUNDO comprobante. Al reintentar se re-envía este mismo <see cref="RequestId"/> y el
    /// servicio fiscal devuelve el que ya emitió, si lo emitió.
    /// </summary>
    public void MarkUnconfirmed(string? reason)
    {
        if (Status == SaleInvoicingStatus.Invoiced)
        {
            return;
        }

        Status = SaleInvoicingStatus.InProgress;
        RejectionReason = Truncate(reason, 500);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// El fisco rechazó por datos: se sabe con certeza que NO se emitió nada, así que el
    /// reintento puede arrancar una secuencia nueva.
    /// </summary>
    public void Reject(Guid? fiscalDocumentId, string? reason)
    {
        if (Status == SaleInvoicingStatus.Invoiced)
        {
            return;
        }

        if (fiscalDocumentId is not null)
        {
            FiscalDocumentId = fiscalDocumentId;
        }

        Status = SaleInvoicingStatus.Rejected;
        RejectionReason = Truncate(reason, 500);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deja sin efecto el comprobante porque se autorizó el que lo compensa: una factura, por su NC;
    /// una NC, por la ND que la neutraliza. Idempotente.
    /// </summary>
    public void Void()
    {
        if (Status == SaleInvoicingStatus.Voided)
        {
            return;
        }

        if (Kind is not (SaleFiscalDocumentKind.Invoice or SaleFiscalDocumentKind.CreditNote))
        {
            throw new InvalidOperationException("Only an invoice or a credit note can be voided.");
        }

        if (Status != SaleInvoicingStatus.Invoiced)
        {
            throw new InvalidOperationException("Only an authorized document can be voided.");
        }

        Status = SaleInvoicingStatus.Voided;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
