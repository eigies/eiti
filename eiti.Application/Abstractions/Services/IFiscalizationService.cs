namespace eiti.Application.Abstractions.Services;

/// <summary>
/// Puerto hacia el servicio de facturación electrónica (eiti-fiscalization).
/// EITI manda hechos ("se hizo esta venta"); el servicio decide tipo de comprobante,
/// numeración y autorización. Acá no se sabe qué organismo fiscal hay atrás.
/// </summary>
public interface IFiscalizationService
{
    /// <summary>False si el servicio no está configurado. Permite no ofrecer facturación en vez de fallar.</summary>
    bool IsEnabled { get; }

    Task<FiscalDocumentResult> RequestDocumentAsync(
        FiscalDocumentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lee el comprobante completo. El callback es una notificación fina: avisa que se resolvió
    /// pero no trae tipo, punto de venta ni vencimiento del CAE — y sin esos datos después no se
    /// puede asociar una nota de crédito. Se consulta el registro autoritativo.
    /// </summary>
    Task<FiscalDocumentResult> GetDocumentAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Descarga el PDF del comprobante. El endpoint del servicio exige la API key de servicio,
    /// así que el front nunca le pega directo: EITI proxea.</summary>
    Task<FiscalPdfResult> DownloadPdfAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>Qué comprobante se pide. <c>Auto</c> deja que el servicio resuelva A o B según el receptor.</summary>
public enum FiscalRequestedDocumentType
{
    Auto,
    Invoice,
    CreditNote,
    DebitNote
}

public enum FiscalReceiverVatCondition
{
    Registered,
    Monotribute,
    FinalConsumer,
    Exempt
}

public sealed record FiscalDocumentRequest(
    string RequestId,
    Guid TenantId,
    FiscalRequestedDocumentType RequestedType,
    FiscalReceiver? Receiver,
    FiscalAmounts Amounts,
    DateOnly Date,
    /// <summary>Null = usa el punto de venta por defecto configurado en el servicio.</summary>
    int? PointOfSale = null,
    FiscalAssociatedDocument? AssociatedDocument = null);

public sealed record FiscalReceiver(
    FiscalReceiverVatCondition VatCondition,
    string? DocType,
    string? DocNumber,
    string? Name);

public sealed record FiscalAmounts(
    decimal Net,
    IReadOnlyCollection<FiscalVatAmount> Vat,
    decimal Exempt,
    decimal Total);

public sealed record FiscalVatAmount(decimal Rate, decimal Base, decimal Amount);

/// <summary>
/// Comprobante que se anula/ajusta. Requerido para notas de crédito y débito.
/// El CUIT del emisor lo completa la capa de infraestructura desde su configuración:
/// es un dato fiscal y no vive en EITI.
/// </summary>
public sealed record FiscalAssociatedDocument(
    string Type,
    int PointOfSale,
    long Number,
    DateOnly Date);

public enum FiscalDocumentOutcome
{
    /// <summary>Autorizado en el fast-path sincrónico.</summary>
    Authorized,
    /// <summary>Encolado: el servicio resuelve y avisa por callback.</summary>
    Queued,
    /// <summary>El fisco rechazó por datos (no se reintenta solo).</summary>
    Rejected,
    /// <summary>No se pudo contactar al servicio o respondió mal. Se puede reintentar.</summary>
    Unavailable
}

public sealed record FiscalDocumentResult(
    FiscalDocumentOutcome Outcome,
    Guid? DocumentId = null,
    string? DocumentType = null,
    int? PointOfSale = null,
    long? Number = null,
    string? AuthorizationCode = null,
    DateTime? ValidUntil = null,
    string? QrUrl = null,
    string? ErrorMessage = null);

public sealed record FiscalPdfResult(
    bool IsSuccess,
    byte[]? Content = null,
    string? ErrorMessage = null);
