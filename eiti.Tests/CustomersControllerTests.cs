using System.Text.Json;
using eiti.Api.Controllers;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Commands.AddCustomerPayment;
using eiti.Application.Features.Customers.Commands.AddCustomerReceipt;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

// El banco de la transferencia tiene que llegar del JSON al comando. En main (772b49b) se agregó al comando y al
// handler pero no al request del controller: el endpoint lo descartaba y los tests del handler pasaban igual.
public sealed class CustomersControllerTests
{
    // Mismas opciones con las que ASP.NET Core lee el body (camelCase, sin distinguir mayúsculas).
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly Error Stop = Error.Validation("Test.Stop", "Solo interesa el comando enviado.");

    [Fact]
    public async Task CobroSuelto_ElBancoDeLaTransferenciaLlegaDelJsonAlComando()
    {
        var request = JsonSerializer.Deserialize<AddCustomerPaymentRequest>(
            """{ "method": 2, "amount": 150000, "date": "2026-10-23", "reference": "Ref. 1", "notes": null, "transferBankId": 7 }""",
            Web)!;
        AddCustomerPaymentCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<AddCustomerPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AddCustomerPaymentResponse>>, CancellationToken>((command, _) => sent = (AddCustomerPaymentCommand)command)
            .ReturnsAsync(Result<AddCustomerPaymentResponse>.Failure(Stop));

        await new CustomersController(sender.Object).AddCustomerPayment(Guid.NewGuid(), request, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.TransferBankId.Should().Be(7);
    }

    [Fact]
    public async Task Recibo_ElBancoDeCadaTransferenciaLlegaDelJsonAlComando()
    {
        var request = JsonSerializer.Deserialize<AddCustomerReceiptRequest>(
            """
            { "date": "2026-10-23", "notes": null, "lines": [
                { "method": 2, "amount": 120000, "reference": "Ref. 1", "transferBankId": 7 },
                { "method": 1, "amount": 30000 } ] }
            """,
            Web)!;
        AddCustomerReceiptCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<AddCustomerReceiptCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AddCustomerReceiptResponse>>, CancellationToken>((command, _) => sent = (AddCustomerReceiptCommand)command)
            .ReturnsAsync(Result<AddCustomerReceiptResponse>.Failure(Stop));

        await new CustomersController(sender.Object).AddCustomerReceipt(Guid.NewGuid(), request, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.Lines.Select(l => l.TransferBankId).Should().Equal(7, null);
    }
}
