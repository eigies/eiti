using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Application.Features.Sales.Commands.CancelSale;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using eiti.Application.Features.Sales.Common;
using Moq;

namespace eiti.Tests;

public sealed class CancelSaleHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRegisterCancellationInTheSaleOriginalCashSession()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var drawerA = CashDrawerId.New();
        var drawerB = CashDrawerId.New();
        var userId = UserId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 99000m, 70000m, null);
        var sale = Sale.Create(
            companyId,
            branchId,
            null,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)],
            [SalePayment.Create(SalePaymentMethod.Cash, 99000m, null)],
            allowOverpayment: true);

        var originalSession = CashSession.Open(companyId, branchId, drawerA, userId, 1000m, null);
        var otherSession = CashSession.Open(companyId, branchId, drawerB, userId, 2000m, null);

        sale.MarkAsPaid(drawerA, originalSession.Id);
        originalSession.RegisterSaleIncome(99000m, sale.Id.Value, userId);

        var stock = BranchProductStock.Create(companyId, branchId, product.Id);

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var saleTransportAssignmentRepository = new Mock<ISaleTransportAssignmentRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var customerPaymentRepository = new Mock<ICustomerPaymentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.SetupGet(x => x.UserId).Returns(userId);

        saleRepository
            .Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        branchProductStockRepository
            .Setup(x => x.GetOrCreateAsync(branchId, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        cashSessionRepository
            .Setup(x => x.GetByIdAsync(originalSession.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);

        var handler = new CancelSaleHandler(
            currentUserService.Object,
            saleRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            saleTransportAssignmentRepository.Object,
            customerRepository.Object,
            customerPaymentRepository.Object,
            new Mock<ICustomerReceiptService>().Object,
            new Mock<ISaleInvoicingService>().Object,
            unitOfWork.Object);

        var result = await handler.Handle(new CancelSaleCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        originalSession.ExpectedClosingAmount.Should().Be(1000m);
        originalSession.Movements.Last().Type.Should().Be(CashMovementType.SaleCancellation);
        otherSession.ExpectedClosingAmount.Should().Be(2000m);
        otherSession.Movements.Should().OnlyContain(m => m.Type == CashMovementType.OpeningFloat);
        cashSessionRepository.Verify(x => x.GetAnyOpenByBranchAsync(
            It.IsAny<BranchId>(),
            It.IsAny<CompanyId>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // Caso real (LACTEOS BARRAZA, 07/10): la venta CC consumió saldo a favor al crearse y se anuló con
    // "Anular los pagos". No hay cobro que revertir, así que ese saldo tiene que volver al cliente.
    [Fact]
    public async Task Handle_ReversePayments_ShouldReturnCreditAppliedWhenTheSaleWasCreated()
    {
        var ctx = new CcCancelContext();
        ctx.Customer.AddCredit(14407.17m);
        var sale = ctx.CreateCcSale(200000m);
        ctx.Customer.ConsumeCredit(14407.17m);
        sale.AddCcPayment(SalePaymentMethod.CustomerCredit, 14407.17m, DateTime.UtcNow, "Saldo a favor aplicado automáticamente");

        var result = await ctx.Handler.Handle(
            new CancelSaleCommand(sale.Id.Value, CcCancellationRefundMode.ReversePayments), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Cancel);
        sale.CcPayments.Should().OnlyContain(p => p.Status == SaleCcPaymentStatus.Cancelled);
        ctx.Customer.CreditBalance.Should().Be(14407.17m);
    }

    // El FIFO de una nota de crédito (o de otra anulación en modo saldo a favor) imputa con CustomerPaymentId
    // vacío: tampoco hay cobro que revertir.
    [Fact]
    public async Task Handle_ReversePayments_ShouldReturnCreditImputedFromACreditNote()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        sale.ApplyCustomerCredit(30000m, DateTime.UtcNow, Guid.Empty, "Saldo a favor aplicado automáticamente", Guid.NewGuid());

        var result = await ctx.Handler.Handle(
            new CancelSaleCommand(sale.Id.Value, CcCancellationRefundMode.ReversePayments), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ctx.Customer.CreditBalance.Should().Be(30000m);
    }

    // La imputación de un cobro real la deshace la reversa del cobro (CustomerPaymentReversal): el handler no la
    // devuelve otra vez.
    [Fact]
    public async Task Handle_ReversePayments_ShouldNotReturnCreditOfAReversedPaymentTwice()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        var payment = CustomerPayment.Create(
            ctx.CompanyId.Value, ctx.Customer.Id.Value, ctx.BranchId.Value, SalePaymentMethod.Transfer,
            50000m, DateTime.UtcNow, null, null, ctx.UserId.Value);
        sale.ApplyCustomerCredit(50000m, DateTime.UtcNow, payment.Id, "Saldo a favor aplicado automáticamente");
        ctx.CustomerPaymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, ctx.CompanyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var result = await ctx.Handler.Handle(
            new CancelSaleCommand(sale.Id.Value, CcCancellationRefundMode.ReversePayments), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ctx.CustomerReceiptService.Verify(x => x.ReversePaymentAsync(
            payment, ctx.Customer, It.IsAny<CashSession?>(), ctx.CompanyId, ctx.UserId, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Customer.CreditBalance.Should().Be(0m);
    }

    // ── Factura emitida a otro receptor que el de la ficha ──────────────────────
    // La NC sale al receptor de la factura; si la ficha cambió después de facturar, el usuario tiene que verlo
    // y confirmarlo antes de anular (MAT-3705: la NC salió a otro CUIT sin que nadie lo viera).

    [Fact]
    public async Task Handle_InvoiceReceiverChanged_ShouldNotCancelWithoutConfirmation()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        ctx.InvoiceIssuedTo(customerMatches: false);

        var result = await ctx.Handler.Handle(new CancelSaleCommand(sale.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Cancel.InvoiceReceiverChanged");
        sale.SaleStatus.Should().NotBe(SaleStatus.Cancel, "nothing is touched before the user confirms");
        ctx.Invoicing.Verify(x => x.IssueCreditNoteAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvoiceReceiverChanged_Confirmed_ShouldCancelAndIssueTheCreditNote()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        ctx.InvoiceIssuedTo(customerMatches: false);

        var result = await ctx.Handler.Handle(
            new CancelSaleCommand(sale.Id.Value, ConfirmInvoiceReceiver: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Cancel);
        ctx.Invoicing.Verify(x => x.IssueCreditNoteAsync(sale, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameInvoiceReceiver_ShouldCancelWithoutAsking()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        ctx.InvoiceIssuedTo(customerMatches: true);

        var result = await ctx.Handler.Handle(new CancelSaleCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Cancel);
    }

    // Anular es una decisión del negocio: sin el servicio fiscal se anula igual, y la NC no sale sin poder
    // verificar el receptor (queda para reintentar desde el detalle).
    [Fact]
    public async Task Handle_FiscalServiceUnavailable_ShouldStillCancel()
    {
        var ctx = new CcCancelContext();
        var sale = ctx.CreateCcSale(200000m);
        ctx.Invoicing.SetupGet(x => x.IsEnabled).Returns(true);
        ctx.Invoicing.Setup(x => x.GetFiscalReceiversAsync(sale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SaleFiscalReceivers.Unavailable([], "No se pudo contactar al servicio de facturación."));

        var result = await ctx.Handler.Handle(new CancelSaleCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.SaleStatus.Should().Be(SaleStatus.Cancel);
    }

    private sealed class CcCancelContext
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public BranchId BranchId { get; } = BranchId.New();
        public UserId UserId { get; } = UserId.New();
        public Customer Customer { get; }
        public Mock<ISaleRepository> SaleRepository { get; } = new();
        public Mock<ICustomerPaymentRepository> CustomerPaymentRepository { get; } = new();
        public Mock<ICustomerReceiptService> CustomerReceiptService { get; } = new();
        public Mock<ISaleInvoicingService> Invoicing { get; } = new();
        public CancelSaleHandler Handler { get; }

        private readonly Mock<IBranchProductStockRepository> _stockRepository = new();

        public CcCancelContext()
        {
            Customer = Customer.Create(CompanyId, "LACTEOS", "BARRAZA", null);

            var currentUserService = new Mock<ICurrentUserService>();
            currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
            currentUserService.SetupGet(x => x.CompanyId).Returns(CompanyId);
            currentUserService.SetupGet(x => x.UserId).Returns(UserId);

            var customerRepository = new Mock<ICustomerRepository>();
            customerRepository
                .Setup(x => x.GetByIdAsync(Customer.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Customer);

            SaleRepository
                .Setup(x => x.ListPendingCcSalesByCustomerAsync(CompanyId, Customer.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            CustomerReceiptService
                .Setup(x => x.EnsureReversibleAloneAsync(
                    It.IsAny<IReadOnlyCollection<CustomerPayment>>(), CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());

            Handler = new CancelSaleHandler(
                currentUserService.Object,
                SaleRepository.Object,
                _stockRepository.Object,
                new Mock<IStockMovementRepository>().Object,
                new Mock<ICashDrawerRepository>().Object,
                new Mock<ICashSessionRepository>().Object,
                new Mock<ISaleTransportAssignmentRepository>().Object,
                customerRepository.Object,
                CustomerPaymentRepository.Object,
                CustomerReceiptService.Object,
                Invoicing.Object,
                new Mock<IUnitOfWork>().Object);
        }

        // Venta CC pendiente (OnHold) de una unidad, con su stock reservado como lo deja CreateCcSale.
        public Sale CreateCcSale(decimal price)
        {
            var product = Product.Create(CompanyId, "BAT-002", "BAT-002", "Contoso", "Bateria", null, price, 70000m, null);
            var sale = Sale.CreateCc(CompanyId, BranchId, Customer.Id, [SaleDetail.Create(product.Id, 1, product.Price)]);

            var stock = BranchProductStock.Create(CompanyId, BranchId, product.Id);
            stock.ApplyManualEntry(1);
            stock.Reserve(1);
            _stockRepository
                .Setup(x => x.GetOrCreateAsync(BranchId, product.Id, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            SaleRepository
                .Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sale);

            _sale = sale;
            return sale;
        }

        private Sale? _sale;

        /// <summary>
        /// La venta tiene factura A vigente a CUIT 30-68225650-4. Si <paramref name="customerMatches"/> es false,
        /// la ficha del cliente hoy dice otro CUIT (el caso de MAT-3705).
        /// </summary>
        public void InvoiceIssuedTo(bool customerMatches)
        {
            var invoice = SaleFiscalDocument.CreateInvoice(CompanyId, _sale!.Id, 1);
            invoice.Authorize(Guid.NewGuid(), "invoiceA", 10, 39, "86428718520200", null, null, DateTime.UtcNow);
            var invoiceReceiver = new FiscalReceiver(FiscalReceiverVatCondition.Registered, "CUIT", "30682256504", "TRANSPORTE OLIVOS S.A -");
            var customerReceiver = customerMatches ? invoiceReceiver : invoiceReceiver with { DocNumber = "30517431431" };
            var printed = new FiscalPrintableDocument("invoiceA", 10, 39, new DateOnly(2026, 10, 21), "86428718520200",
                new DateOnly(2026, 10, 31), "https://qr",
                new FiscalIssuer("SOLER EMILIANO JULIAN", "20397583857", FiscalReceiverVatCondition.Registered, null, new DateOnly(2024, 10, 1)),
                new FiscalAmounts(165289.26m, [new FiscalVatAmount(21m, 165289.26m, 34710.74m)], 0m, 200000m),
                invoiceReceiver,
                null);

            Invoicing.SetupGet(x => x.IsEnabled).Returns(true);
            Invoicing.Setup(x => x.GetFiscalReceiversAsync(_sale, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SaleFiscalReceivers(true, null, [invoice], invoice, printed, customerReceiver, null, null));
            Invoicing.Setup(x => x.IssueCreditNoteAsync(_sale, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced));
        }
    }
}
