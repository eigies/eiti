using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using eiti.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eiti.Infrastructure.Services;

/// <summary>
/// Cliente HTTP del servicio de facturación electrónica.
/// Traduce el modelo de EITI al contrato REST de eiti-fiscalization.
/// </summary>
public sealed class FiscalizationService : IFiscalizationService
{
    // El servicio serializa enums como strings camelCase; hay que espejarlo o rechaza el body.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<FiscalizationOptions> _options;
    private readonly ILogger<FiscalizationService> _logger;

    public FiscalizationService(
        HttpClient httpClient,
        IOptions<FiscalizationOptions> options,
        ILogger<FiscalizationService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.Value.IsConfigured;

    public async Task<FiscalDocumentResult> RequestDocumentAsync(
        FiscalDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            _logger.LogWarning("Fiscalization service is not configured; skipping document request.");
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "El servicio de facturación no está configurado.");
        }

        var payload = new
        {
            requestId = request.RequestId,
            tenantId = request.TenantId,
            regime = options.Regime,
            pointOfSale = request.PointOfSale ?? options.DefaultPointOfSale,
            requestedType = request.RequestedType,
            receiver = request.Receiver is null
                ? null
                : new
                {
                    vatCondition = request.Receiver.VatCondition,
                    docType = request.Receiver.DocType,
                    docNumber = request.Receiver.DocNumber,
                    name = request.Receiver.Name
                },
            amounts = new
            {
                net = request.Amounts.Net,
                vat = request.Amounts.Vat.Select(x => new { rate = x.Rate, @base = x.Base, amount = x.Amount }),
                exempt = request.Amounts.Exempt,
                total = request.Amounts.Total
            },
            date = request.Date.ToString("yyyy-MM-dd"),
            callbackUrl = options.CallbackUrl,
            associatedDocument = request.AssociatedDocument is null
                ? null
                : new
                {
                    type = request.AssociatedDocument.Type,
                    pointOfSale = request.AssociatedDocument.PointOfSale,
                    number = request.AssociatedDocument.Number,
                    date = request.AssociatedDocument.Date.ToString("yyyy-MM-dd")
                }
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri(options, "api/fiscal-documents"))
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            httpRequest.Headers.Add("X-Api-Key", options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 409 = idempotencia: el comprobante ya existía. Devuelve el existente, no es un error.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            {
                return MapResponse(body);
            }

            _logger.LogWarning(
                "Fiscalization request failed. Status: {StatusCode}, Body: {Body}",
                (int)response.StatusCode,
                body);

            // 400/422 son datos inválidos: reintentar igual no lo arregla.
            var isDataError = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;
            return new FiscalDocumentResult(
                isDataError ? FiscalDocumentOutcome.Rejected : FiscalDocumentOutcome.Unavailable,
                ErrorMessage: ExtractErrorMessage(body, (int)response.StatusCode));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach the fiscalization service.");
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "No se pudo contactar al servicio de facturación.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while requesting a fiscal document.");
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "Error inesperado al solicitar el comprobante.");
        }
    }

    public async Task<FiscalDocumentResult> GetDocumentAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "El servicio de facturación no está configurado.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                BuildUri(options, $"api/fiscal-documents/{documentId}?tenantId={tenantId}"));
            httpRequest.Headers.Add("X-Api-Key", options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Could not read fiscal document {DocumentId}. Status: {StatusCode}",
                    documentId,
                    (int)response.StatusCode);
                return new FiscalDocumentResult(
                    FiscalDocumentOutcome.Unavailable,
                    ErrorMessage: ExtractErrorMessage(body, (int)response.StatusCode));
            }

            return MapResponse(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading fiscal document {DocumentId}.", documentId);
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "Error inesperado al consultar el comprobante.");
        }
    }

    public async Task<FiscalPdfResult> DownloadPdfAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            return new FiscalPdfResult(false, ErrorMessage: "El servicio de facturación no está configurado.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                BuildUri(options, $"api/fiscal-documents/{documentId}/pdf?tenantId={tenantId}"));
            httpRequest.Headers.Add("X-Api-Key", options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Fiscal PDF download failed. Document: {DocumentId}, Status: {StatusCode}",
                    documentId,
                    (int)response.StatusCode);
                return new FiscalPdfResult(false, ErrorMessage: "No se pudo obtener el comprobante.");
            }

            return new FiscalPdfResult(true, await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while downloading the fiscal PDF.");
            return new FiscalPdfResult(false, ErrorMessage: "Error inesperado al descargar el comprobante.");
        }
    }

    public async Task<FiscalPrintableDocumentResult> GetPrintableDocumentAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            return new FiscalPrintableDocumentResult(false, ErrorMessage: "El servicio de facturación no está configurado.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                BuildUri(options, $"api/fiscal-documents/{documentId}?tenantId={tenantId}"));
            httpRequest.Headers.Add("X-Api-Key", options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Could not read printable fiscal document {DocumentId}. Status: {StatusCode}",
                    documentId,
                    (int)response.StatusCode);
                return new FiscalPrintableDocumentResult(false, ErrorMessage: ExtractErrorMessage(body, (int)response.StatusCode));
            }

            var payload = JsonSerializer.Deserialize<PrintableDocumentPayload>(body, JsonOptions);
            // Solo un comprobante autorizado se imprime: sin CAE no es un documento fiscal.
            if (payload is not { Status: "authorized", Type: not null, Number: not null, AuthorizationCode: not null,
                    ValidUntil: not null, Qr: not null, Issuer: not null, Amounts: not null })
            {
                return new FiscalPrintableDocumentResult(false, ErrorMessage: "El comprobante no está autorizado o le faltan datos para imprimirlo.");
            }

            return new FiscalPrintableDocumentResult(true, new FiscalPrintableDocument(
                payload.Type,
                payload.PointOfSale,
                payload.Number.Value,
                payload.Date,
                payload.AuthorizationCode,
                payload.ValidUntil.Value,
                payload.Qr,
                payload.Issuer,
                payload.Amounts,
                payload.Receiver,
                payload.AssociatedDocument));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach the fiscalization service to print {DocumentId}.", documentId);
            return new FiscalPrintableDocumentResult(false, ErrorMessage: "No se pudo contactar al servicio de facturación.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading printable fiscal document {DocumentId}.", documentId);
            return new FiscalPrintableDocumentResult(false, ErrorMessage: "Error inesperado al consultar el comprobante.");
        }
    }

    private static Uri BuildUri(FiscalizationOptions options, string relativePath) =>
        new(new Uri(options.BaseUrl!.TrimEnd('/') + "/"), relativePath);

    private static FiscalDocumentResult MapResponse(string body)
    {
        var payload = JsonSerializer.Deserialize<FiscalDocumentResponsePayload>(body, JsonOptions);
        if (payload is null)
        {
            return new FiscalDocumentResult(
                FiscalDocumentOutcome.Unavailable,
                ErrorMessage: "El servicio de facturación devolvió una respuesta vacía.");
        }

        var outcome = payload.Status switch
        {
            "authorized" => FiscalDocumentOutcome.Authorized,
            "pending" => FiscalDocumentOutcome.Queued,
            "rejected" => FiscalDocumentOutcome.Rejected,
            // "failed" es la DLQ del servicio: agotó reintentos técnicos.
            "failed" => FiscalDocumentOutcome.Unavailable,
            _ => FiscalDocumentOutcome.Unavailable
        };

        return new FiscalDocumentResult(
            outcome,
            payload.DocumentId,
            payload.Type,
            payload.PointOfSale,
            payload.Number,
            payload.AuthorizationCode,
            payload.ValidUntil?.ToDateTime(TimeOnly.MinValue),
            payload.Qr);
    }

    /// <summary>
    /// Rechazos del servicio que el vendedor puede resolver solo. El servicio contesta en términos
    /// técnicos y en inglés; lo que llega a la venta tiene que decir qué dato completar.
    /// </summary>
    private static readonly Dictionary<string, string> KnownErrorMessages = new(StringComparer.Ordinal)
    {
        ["Arca.ReceiverCuitRequired"] =
            "El cliente es Responsable Inscripto o Monotributista: para facturarle hay que cargar su CUIT en la ficha del cliente.",
        ["Arca.ReceiverIdentificationRequired"] =
            "El monto supera el tope de ARCA para facturar a un consumidor final sin identificar. Asigná un cliente a la venta con su DNI o CUIT cargado y volvé a facturar."
    };

    private static string ExtractErrorMessage(string body, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"El servicio de facturación respondió {statusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.String &&
                KnownErrorMessages.TryGetValue(code.GetString()!, out var known))
            {
                return known;
            }

            if (document.RootElement.TryGetProperty("description", out var description))
            {
                return description.GetString() ?? body;
            }

            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0 &&
                errors[0].TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // El cuerpo no era JSON: se devuelve tal cual, recortado.
        }

        return body.Length > 300 ? body[..300] : body;
    }

    private sealed record FiscalDocumentResponsePayload(
        Guid DocumentId,
        string? Status,
        string? Type,
        int? PointOfSale,
        long? Number,
        string? AuthorizationCode,
        DateOnly? ValidUntil,
        string? Qr,
        bool IsQueued,
        bool IsDuplicate);

    // Respuesta de GET /api/fiscal-documents/{id}: los campos del alta más emisor, fecha, importes,
    // receptor y comprobante asociado (los del pedido que se autorizó).
    private sealed record PrintableDocumentPayload(
        string? Status,
        string? Type,
        int PointOfSale,
        long? Number,
        string? AuthorizationCode,
        DateOnly? ValidUntil,
        string? Qr,
        DateOnly Date,
        FiscalIssuer? Issuer,
        FiscalAmounts? Amounts,
        FiscalReceiver? Receiver,
        FiscalAssociatedDocument? AssociatedDocument);
}
