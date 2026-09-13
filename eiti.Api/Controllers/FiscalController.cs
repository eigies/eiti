using System.Text.Json;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.ApplyFiscalCallback;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

/// <summary>
/// Recibe las resoluciones diferidas del servicio de facturación electrónica.
/// </summary>
[ApiController]
[Route("api/fiscal")]
public sealed class FiscalController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISender _sender;
    private readonly IFiscalCallbackSignatureValidator _signatureValidator;
    private readonly ILogger<FiscalController> _logger;

    public FiscalController(
        ISender sender,
        IFiscalCallbackSignatureValidator signatureValidator,
        ILogger<FiscalController> logger)
    {
        _sender = sender;
        _signatureValidator = signatureValidator;
        _logger = logger;
    }

    /// <summary>
    /// Callback del servicio fiscal. Es anónimo respecto del JWT de usuario a propósito: no lo
    /// dispara una persona. Se autentica con la firma HMAC del cuerpo, que solo puede producir
    /// quien conoce el secreto compartido.
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        // El HMAC se calcula sobre los bytes EXACTOS que llegaron. Si se dejara que el binding
        // de ASP.NET deserialice y después se re-serializara, la firma no cerraría nunca.
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var rawBody = buffer.ToArray();

        var isValid = _signatureValidator.IsValid(
            rawBody,
            Request.Headers["X-Timestamp"].ToString(),
            Request.Headers["X-Signature"].ToString());

        if (!isValid)
        {
            _logger.LogWarning("Rejected a fiscal callback with an invalid or expired signature.");
            return Unauthorized();
        }

        FiscalCallbackPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FiscalCallbackPayload>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Received a fiscal callback with a malformed body.");
            return BadRequest();
        }

        if (payload is null || payload.DocumentId == Guid.Empty)
        {
            return BadRequest();
        }

        var result = await _sender.Send(
            new ApplyFiscalCallbackCommand(
                payload.DocumentId,
                payload.SaleId,
                payload.Status,
                payload.AuthorizationCode,
                payload.Number,
                payload.Qr,
                payload.RejectionReason),
            cancellationToken);

        // Si acá se devolviera un error, el servicio fiscal reintentaría ocho veces con backoff.
        // Solo se responde error cuando reintentar puede servir de algo.
        return result.IsSuccess ? Ok() : StatusCode(StatusCodes.Status500InternalServerError);
    }

    private sealed record FiscalCallbackPayload(
        Guid DocumentId,
        string? SaleId,
        string? Status,
        string? AuthorizationCode,
        long? Number,
        string? Qr,
        string? PdfUrl,
        string? RejectionReason);
}
