using eiti.Api.Controllers;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Commands.CancelPurchase;
using eiti.Application.Features.Sales.Commands.CancelSale;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

// La confirmación de anular también las notas de crédito vinculadas viaja por query string
// (?cancelLinkedCreditNotes=true). Un campo nuevo en el comando no está terminado hasta que el endpoint lo pasa:
// los tests de handler no lo prueban. El argumento con nombre fija también el nombre del parámetro que bindea.
public sealed class CancelWithCreditNotesControllerTests
{
    private static readonly Error Stop = Error.Validation("Test.Stop", "Solo interesa el comando enviado.");

    [Fact]
    public async Task AnularCompra_LaConfirmacionDeLasNotasLlegaAlComando()
    {
        CancelPurchaseCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CancelPurchaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result>, CancellationToken>((command, _) => sent = (CancelPurchaseCommand)command)
            .ReturnsAsync(Result.Failure(Stop));

        await new PurchasesController(sender.Object).CancelPurchase(
            Guid.NewGuid(), PurchaseCancellationRefundMode.Credit, cancelLinkedCreditNotes: true, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.RefundMode.Should().Be(PurchaseCancellationRefundMode.Credit);
        sent.CancelLinkedCreditNotes.Should().BeTrue();
    }

    [Fact]
    public async Task AnularVenta_LaConfirmacionDeLasNotasLlegaAlComando()
    {
        CancelSaleCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CancelSaleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result>, CancellationToken>((command, _) => sent = (CancelSaleCommand)command)
            .ReturnsAsync(Result.Failure(Stop));

        await new SalesController(sender.Object).CancelSale(
            Guid.NewGuid(), refundMode: 1, confirmInvoiceReceiver: true, cancelLinkedCreditNotes: true, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.RefundMode.Should().Be(CcCancellationRefundMode.Credit);
        sent.ConfirmInvoiceReceiver.Should().BeTrue();
        sent.CancelLinkedCreditNotes.Should().BeTrue();
    }
}
