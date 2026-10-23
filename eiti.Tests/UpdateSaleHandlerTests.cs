using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class UpdateSaleHandlerTests
{
    // Regresión: cobrar una venta CC desde acá (en vez de la cuenta del cliente) generaba un
    // SalePayment + ingreso de caja "fantasma" que no quedaba reflejado en la cuenta corriente
    // (bug real detectado en producción, venta SMA-042).
    [Fact]
    public async Task Handle_ShouldRejectMarkingPaid_WhenSaleIsCuentaCorriente()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var ccSale = Sale.CreateCc(
            companyId,
            branch.Id,
            CustomerId.New(),
            [SaleDetail.Create(product.Id, 1, product.Price)]);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPay)).Returns(true);

        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(repository => repository.GetByIdAsync(ccSale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ccSale);

        var handler = new UpdateSaleHandler(
            currentUserService.Object,
            saleRepository.Object,
            new Mock<ICustomerRepository>().Object,
            new Mock<IProductRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object,
            new Mock<IStockMovementRepository>().Object,
            new Mock<ICashDrawerRepository>().Object,
            new Mock<ICashSessionRepository>().Object,
            new Mock<ISaleTransportAssignmentRepository>().Object,
            new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object,
            NoFiscalDocuments(),
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new UpdateSaleCommand(
                ccSale.Id.Value,
                null,
                (int)SaleStatus.Paid,
                false,
                null,
                [new UpdateSaleDetailItemRequest(product.Id.Value, 1)],
                [],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Update.CannotChargeCuentaCorriente");
    }

    [Fact]
    public async Task Handle_ShouldRejectCardPayment_WhenBankIsNotEnabledForCard()
    {
        var companyId = CompanyId.New();
        var scenario = CreateScenario(companyId);
        var bank = Bank.Create(companyId, "Banco tarjeta", useForCard: false, useForTransfer: true, useForCheque: true);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdAsync(bank.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);
        var handler = CreateHandler(companyId, scenario, bankRepository.Object);

        var result = await handler.Handle(
            new UpdateSaleCommand(
                scenario.Sale.Id.Value,
                null,
                (int)SaleStatus.OnHold,
                false,
                null,
                [new UpdateSaleDetailItemRequest(scenario.Product.Id.Value, 1)],
                [new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 100m, null, CardBankId: bank.Id, CardCuotas: 1)],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Update.CardBankInvalid");
    }

    [Fact]
    public async Task Handle_ShouldRejectTransferPayment_WhenBankIsNotEnabledForTransfer()
    {
        var companyId = CompanyId.New();
        var scenario = CreateScenario(companyId);
        var bank = Bank.Create(companyId, "Banco transferencia", useForCard: true, useForTransfer: false, useForCheque: true);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdAsync(bank.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);
        var handler = CreateHandler(companyId, scenario, bankRepository.Object);

        var result = await handler.Handle(
            new UpdateSaleCommand(
                scenario.Sale.Id.Value,
                null,
                (int)SaleStatus.OnHold,
                false,
                null,
                [new UpdateSaleDetailItemRequest(scenario.Product.Id.Value, 1)],
                [new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Transfer, 100m, null, TransferBankId: bank.Id)],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Update.TransferBankInvalid");
    }

    // Regresión: MAT-3358 se facturó al crearla por $ 154.900 y después se editó a $ 174.900. La
    // factura quedó con el importe viejo y la venta con el nuevo, sin NC ni ND que lo explique.
    [Fact]
    public async Task Handle_ShouldRejectAmountChange_WhenSaleHasLiveInvoice()
    {
        var companyId = CompanyId.New();
        var scenario = CreateScenario(companyId);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = CreateHandler(companyId, scenario, new Mock<IBankRepository>().Object,
            [AuthorizedInvoice(companyId, scenario.Sale)], unitOfWork);

        var result = await handler.Handle(
            UpdateCommand(scenario, quantity: 2),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Update.InvoicedAmountChange");
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRejectAmountChange_WhenInvoicingIsInProgress()
    {
        var companyId = CompanyId.New();
        var scenario = CreateScenario(companyId);
        var handler = CreateHandler(companyId, scenario, new Mock<IBankRepository>().Object,
            [SaleFiscalDocument.CreateInvoice(companyId, scenario.Sale.Id, 1)]);

        var result = await handler.Handle(
            UpdateCommand(scenario, quantity: 2),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Update.InvoicedAmountChange");
    }

    [Fact]
    public async Task Handle_ShouldAllowUpdate_WhenSaleHasLiveInvoiceAndAmountDoesNotChange()
    {
        var companyId = CompanyId.New();
        var scenario = CreateScenario(companyId);
        var handler = CreateHandler(companyId, scenario, new Mock<IBankRepository>().Object,
            [AuthorizedInvoice(companyId, scenario.Sale)]);

        var result = await handler.Handle(
            UpdateCommand(scenario, quantity: 1) with { ContactPhone = "2964 123456" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static ISaleFiscalDocumentRepository NoFiscalDocuments()
    {
        var repository = new Mock<ISaleFiscalDocumentRepository>();
        repository
            .Setup(fiscalDocuments => fiscalDocuments.ListBySaleAsync(It.IsAny<SaleId>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return repository.Object;
    }

    private static UpdateSaleCommand UpdateCommand(UpdateSaleScenario scenario, int quantity) =>
        new(
            scenario.Sale.Id.Value,
            null,
            (int)SaleStatus.OnHold,
            false,
            null,
            [new UpdateSaleDetailItemRequest(scenario.Product.Id.Value, quantity)],
            [],
            []);

    private static SaleFiscalDocument AuthorizedInvoice(CompanyId companyId, eiti.Domain.Sales.Sale sale)
    {
        var invoice = SaleFiscalDocument.CreateInvoice(companyId, sale.Id, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceB", 10, 1, "86428447195933", DateTime.UtcNow.AddDays(10), "https://qr", DateTime.UtcNow);
        return invoice;
    }

    private static UpdateSaleScenario CreateScenario(CompanyId companyId)
    {
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var sale = Sale.Create(
            companyId,
            branch.Id,
            null,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)]);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);
        stock.Reserve(1);

        return new UpdateSaleScenario(branch, product, sale, stock);
    }

    private static UpdateSaleHandler CreateHandler(
        CompanyId companyId,
        UpdateSaleScenario scenario,
        IBankRepository bankRepository,
        IReadOnlyList<SaleFiscalDocument>? fiscalDocuments = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var fiscalDocumentRepository = new Mock<ISaleFiscalDocumentRepository>();
        unitOfWork ??= new Mock<IUnitOfWork>();

        fiscalDocumentRepository
            .Setup(repository => repository.ListBySaleAsync(scenario.Sale.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiscalDocuments ?? []);

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        saleRepository
            .Setup(repository => repository.GetByIdAsync(scenario.Sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario.Sale);

        productRepository
            .Setup(repository => repository.GetByIdAsync(scenario.Product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario.Product);

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(scenario.Branch.Id, scenario.Product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario.Stock);

        return new UpdateSaleHandler(
            currentUserService.Object,
            saleRepository.Object,
            customerRepository.Object,
            productRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<ISaleTransportAssignmentRepository>().Object,
            new Mock<IAddressRepository>().Object,
            bankRepository,
            fiscalDocumentRepository.Object,
            unitOfWork.Object);
    }

    private sealed record UpdateSaleScenario(
        Branch Branch,
        Product Product,
        eiti.Domain.Sales.Sale Sale,
        BranchProductStock Stock);
}
