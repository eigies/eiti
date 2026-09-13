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
    /// Solo en una nota de crédito: el comprobante que anula. Una NC anula una FACTURA, no una
    /// venta — es lo que el fisco asocia (CbtesAsoc). Null en una factura.
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
    public string RequestId => $"{SaleId.Value}:{(Kind == SaleFiscalDocumentKind.Invoice ? "inv" : "cn")}:{Sequence}";

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
    /// Anula la factura porque su nota de crédito quedó autorizada. Idempotente.
    /// </summary>
    public void Void()
    {
        if (Status == SaleInvoicingStatus.Voided)
        {
            return;
        }

        if (Kind != SaleFiscalDocumentKind.Invoice)
        {
            throw new InvalidOperationException("Only an invoice can be voided.");
        }

        if (Status != SaleInvoicingStatus.Invoiced)
        {
            throw new InvalidOperationException("Only an authorized invoice can be voided.");
        }

        Status = SaleInvoicingStatus.Voided;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
