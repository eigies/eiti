using System.Net;
using System.Text;
using eiti.Application.Abstractions.Services;
using eiti.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eiti.Tests;

/// <summary>
/// El contrato con eiti-fiscalization cruza dos repos: este test fija la forma exacta del JSON que
/// devuelve GET /api/fiscal-documents/{id} (enums en camelCase, fechas como DateOnly).
/// </summary>
public sealed class FiscalizationServicePrintableTests
{
    private const string AuthorizedCreditNote = """
        {"documentId":"0ee0c725-cb3d-4c57-adf9-42871a203913","status":"authorized","type":"creditNoteB","pointOfSale":1,
         "number":5,"authorizationCode":"86390928300000","validUntil":"2026-10-05","qr":"https://www.arca.gob.ar/fe/qr/?p=abc",
         "isQueued":false,"date":"2026-09-25",
         "issuer":{"legalName":"Soler Emiliano Julian","cuit":"20397583857","vatCondition":"registered","iibb":null,"activityStartDate":"2015-01-01"},
         "amounts":{"net":1000.00,"vat":[{"rate":21,"base":1000.00,"amount":210.00}],"exempt":0,"total":1210.00},
         "receiver":{"vatCondition":"finalConsumer","docType":"DNI","docNumber":"30111222","name":"Juan Perez"},
         "associatedDocument":{"type":"invoiceB","pointOfSale":1,"number":3,"date":"2026-09-25","issuerCuit":null}}
        """;

    [Fact]
    public async Task Printable_document_is_read_from_the_fiscal_service_contract()
    {
        var handler = new StubHandler(HttpStatusCode.OK, AuthorizedCreditNote);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.Parse("0ee0c725-cb3d-4c57-adf9-42871a203913");

        var result = await Service(handler).GetPrintableDocumentAsync(tenantId, documentId);

        result.IsSuccess.Should().BeTrue();
        handler.RequestUri.Should().Be($"http://fiscal.local/api/fiscal-documents/{documentId}?tenantId={tenantId}");
        var document = result.Document!;
        (document.Type, document.PointOfSale, document.Number, document.Date).Should().Be(("creditNoteB", 1, 5L, new DateOnly(2026, 9, 25)));
        document.AuthorizationExpiry.Should().Be(new DateOnly(2026, 10, 5));
        document.Issuer.VatCondition.Should().Be(FiscalReceiverVatCondition.Registered);
        document.Amounts.Vat.Should().ContainSingle(x => x.Rate == 21m && x.Amount == 210m);
        document.Receiver.Should().Be(new FiscalReceiver(FiscalReceiverVatCondition.FinalConsumer, "DNI", "30111222", "Juan Perez"));
        document.AssociatedDocument.Should().Be(new FiscalAssociatedDocument("invoiceB", 1, 3, new DateOnly(2026, 9, 25)));
    }

    [Fact]
    public async Task A_document_without_authorization_is_not_printable()
    {
        const string pending = """
            {"documentId":"0ee0c725-cb3d-4c57-adf9-42871a203913","status":"pending","type":"invoiceB","pointOfSale":1,
             "number":null,"authorizationCode":null,"validUntil":null,"qr":null,"isQueued":true,"date":"2026-09-25",
             "issuer":null,"amounts":{"net":1000,"vat":[],"exempt":0,"total":1210}}
            """;

        var result = await Service(new StubHandler(HttpStatusCode.OK, pending)).GetPrintableDocumentAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    private static FiscalizationService Service(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new FiscalizationOptions { BaseUrl = "http://fiscal.local", ApiKey = "test-key" }),
        NullLogger<FiscalizationService>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
