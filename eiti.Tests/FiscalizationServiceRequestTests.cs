using System.Net;
using System.Text;
using System.Text.Json;
using eiti.Application.Abstractions.Services;
using eiti.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eiti.Tests;

/// <summary>
/// Lo que EITI le manda a eiti-fiscalization al pedir un comprobante.
/// </summary>
public sealed class FiscalizationServiceRequestTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Without_a_configured_callback_url_the_request_carries_null(string? configured)
    {
        // appsettings.json trae CallbackUrl = "": mandarlo tal cual hacia que el servicio respondiera 500.
        var handler = new CapturingHandler();

        await Service(handler, configured).RequestDocumentAsync(Request());

        CallbackUrl(handler).ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_configured_callback_url_is_sent()
    {
        var handler = new CapturingHandler();

        await Service(handler, "https://api.eiticloud.com/api/fiscal/callback").RequestDocumentAsync(Request());

        CallbackUrl(handler).GetString().Should().Be("https://api.eiticloud.com/api/fiscal/callback");
    }

    [Fact]
    public async Task Without_a_point_of_sale_none_is_sent_so_the_service_uses_the_issuer_one()
    {
        // Cada cliente tiene su punto de venta en ARCA: EITI ya no manda un número global.
        var handler = new CapturingHandler();

        await Service(handler, null).RequestDocumentAsync(Request());

        JsonDocument.Parse(handler.Body!).RootElement.GetProperty("pointOfSale").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_given_point_of_sale_is_sent_as_is()
    {
        // La nota de crédito va al punto de venta de la factura que anula.
        var handler = new CapturingHandler();

        await Service(handler, null).RequestDocumentAsync(Request() with { PointOfSale = 5 });

        JsonDocument.Parse(handler.Body!).RootElement.GetProperty("pointOfSale").GetInt32().Should().Be(5);
    }

    private static JsonElement CallbackUrl(CapturingHandler handler) =>
        JsonDocument.Parse(handler.Body!).RootElement.GetProperty("callbackUrl");

    private static FiscalDocumentRequest Request() => new(
        "sale-1:inv:1", Guid.NewGuid(), FiscalRequestedDocumentType.Auto, null,
        new FiscalAmounts(100m, [new FiscalVatAmount(21m, 100m, 21m)], 0m, 121m), new DateOnly(2026, 9, 28));

    private static FiscalizationService Service(HttpMessageHandler handler, string? callbackUrl) => new(
        new HttpClient(handler),
        Options.Create(new FiscalizationOptions { BaseUrl = "http://fiscal.local", ApiKey = "test-key", CallbackUrl = callbackUrl }),
        NullLogger<FiscalizationService>.Instance);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"documentId":"0ee0c725-cb3d-4c57-adf9-42871a203913","status":"authorized","type":"invoiceB","pointOfSale":1,"number":1,"authorizationCode":"1","validUntil":"2026-10-05","qr":"q","isQueued":false,"isDuplicate":false}""",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
