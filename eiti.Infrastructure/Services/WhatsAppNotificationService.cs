using System.Net.Http.Json;
using eiti.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eiti.Infrastructure.Services;

public sealed class WhatsAppNotificationService : IWhatsAppNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<WhatsAppDispatchOptions> _options;
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(
        HttpClient httpClient,
        IOptions<WhatsAppDispatchOptions> options,
        ILogger<WhatsAppNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<WhatsAppSendResult> SendSalePaidMessageAsync(
        string fromPhone,
        string toPhone,
        string customerName,
        decimal totalAmount,
        string companyName,
        CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(customerName, totalAmount, companyName);
        var dispatchUrl = _options.Value.DispatchUrl;

        if (string.IsNullOrWhiteSpace(dispatchUrl))
        {
            _logger.LogWarning("WhatsApp dispatch URL is not configured.");
            return new WhatsAppSendResult(false, "WhatsApp dispatch URL is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, dispatchUrl)
            {
                Content = JsonContent.Create(new
                {
                    from = fromPhone,
                    to = toPhone,
                    message
                })
            };

            if (!string.IsNullOrWhiteSpace(_options.Value.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.Value.ApiKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "WhatsApp dispatch failed. Status: {StatusCode}, Body: {Body}",
                    (int)response.StatusCode,
                    body);

                return new WhatsAppSendResult(
                    false,
                    $"WhatsApp provider returned status {(int)response.StatusCode}.");
            }

            return new WhatsAppSendResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending WhatsApp message.");
            return new WhatsAppSendResult(false, "Unexpected error while sending WhatsApp message.");
        }
    }

    private static string BuildMessage(string customerName, decimal totalAmount, string companyName)
    {
        var safeName = string.IsNullOrWhiteSpace(customerName) ? "cliente" : customerName.Trim();
        return $"Hola {safeName}, registramos el pago de tu compra por {totalAmount:0.00} en {companyName}. Gracias.";
    }
}
