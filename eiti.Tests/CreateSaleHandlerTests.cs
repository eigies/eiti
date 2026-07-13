using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CreateSaleHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUseManualTradeInAmount_WhenProductAllowsManualValueInSale()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var saleProduct = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var tradeInProduct = Product.Create(companyId, "TRADE-001", "TRADE-001", "Generico", "Usado recibido", null, 0m, 0m, null, true);
        var stock = BranchProductStock.Create(companyId, branch.Id, saleProduct.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { saleProduct, tradeInProduct });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, saleProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object,
            branchRepository.Object,
            customerRepository.Object,
            productRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            saleRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object,
            new Mock<IChequeRepository>().Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(
                branch.Id.Value,
                null,
                1,
                false,
                null,
                [new CreateSaleDetailItemRequest(saleProduct.Id.Value, 1)],
                [],
                [new CreateSaleTradeInItemRequest(tradeInProduct.Id.Value, 1, 45m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TradeIns.Should().ContainSingle();
        result.Value.TradeIns[0].Amount.Should().Be(45m);
        persistedSale.Should().NotBeNull();
        persistedSale!.TradeIns.Should().ContainSingle();
        persistedSale.TradeIns.Single().Amount.Should().Be(45m);
    }

    [Fact]
    public async Task Handle_ShouldRejectTradeIn_WhenProductDoesNotAllowManualValueInSale()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var saleProduct = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var regularProduct = Product.Create(companyId, "USED-001", "USED-001", "Generico", "Producto comun", null, 50m, 30m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, saleProduct.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { saleProduct, regularProduct });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, saleProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        var handler = new CreateSaleHandler(
            currentUserService.Object,
            branchRepository.Object,
            customerRepository.Object,
            productRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            saleRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IAddressRepository>().Object,
            new Mock<IBankRepository>().Object,
            new Mock<IChequeRepository>().Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(
                branch.Id.Value,
                null,
                1,
                false,
                null,
                [new CreateSaleDetailItemRequest(saleProduct.Id.Value, 1)],
                [],
                [new CreateSaleTradeInItemRequest(regularProduct.Id.Value, 1, 45m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Sales.Create.TradeInManualValueNotAllowed");
    }

    [Fact]
    public async Task Handle_ShouldUseOverridePrice_WhenUserHasPriceOverridePermission()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPriceOverride)).Returns(true);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object, branchRepository.Object, customerRepository.Object,
            productRepository.Object, branchProductStockRepository.Object, stockMovementRepository.Object,
            saleRepository.Object, new Mock<ICashDrawerRepository>().Object, cashSessionRepository.Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1, 50m)], [], []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistedSale.Should().NotBeNull();
        persistedSale!.Details.Single().UnitPrice.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_ShouldAllowZeroPrice_WhenUserHasPriceOverridePermission()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPriceOverride)).Returns(true);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object, branchRepository.Object, customerRepository.Object,
            productRepository.Object, branchProductStockRepository.Object, stockMovementRepository.Object,
            saleRepository.Object, new Mock<ICashDrawerRepository>().Object, cashSessionRepository.Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1, 0m)], [], []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistedSale.Should().NotBeNull();
        persistedSale!.Details.Single().UnitPrice.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOverridePrice_WhenUserLacksPermission()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPriceOverride)).Returns(false);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object, branchRepository.Object, customerRepository.Object,
            productRepository.Object, branchProductStockRepository.Object, stockMovementRepository.Object,
            saleRepository.Object, new Mock<ICashDrawerRepository>().Object, cashSessionRepository.Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1, 50m)], [], []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistedSale.Should().NotBeNull();
        persistedSale!.Details.Single().UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_ShouldUseProductPrice_WhenNoOverrideAndNoPermission()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPriceOverride)).Returns(false);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object, branchRepository.Object, customerRepository.Object,
            productRepository.Object, branchProductStockRepository.Object, stockMovementRepository.Object,
            saleRepository.Object, new Mock<ICashDrawerRepository>().Object, cashSessionRepository.Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)], [], []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistedSale.Should().NotBeNull();
        persistedSale!.Details.Single().UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_ShouldUseBranchOverride_WhenStockHasPricingOverride()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);
        // Override de sucursal: precio 80, costo 50 (distintos del global 100/70).
        stock.SetPricing(50m, 80m);

        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        eiti.Domain.Sales.Sale? persistedSale = null;

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.SalesPriceOverride)).Returns(false);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Callback<eiti.Domain.Sales.Sale, CancellationToken>((sale, _) => persistedSale = sale)
            .Returns(Task.CompletedTask);

        var handler = new CreateSaleHandler(
            currentUserService.Object, branchRepository.Object, customerRepository.Object,
            productRepository.Object, branchProductStockRepository.Object, stockMovementRepository.Object,
            saleRepository.Object, new Mock<ICashDrawerRepository>().Object, cashSessionRepository.Object, new Mock<IAddressRepository>().Object, new Mock<IBankRepository>().Object, new Mock<IChequeRepository>().Object, unitOfWork.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(branch.Id.Value, null, 1, false, null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)], [], []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persistedSale.Should().NotBeNull();
        persistedSale!.Details.Single().UnitPrice.Should().Be(80m);
        persistedSale.Details.Single().UnitCost.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_ShouldRejectCardPayment_WhenBankIsNotEnabledForCard()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);
        var bank = Bank.Create(companyId, "Banco tarjeta", useForCard: false, useForTransfer: true, useForCheque: true);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bank> { bank });
        var handler = CreateHandler(companyId, branch, product, stock, bankRepository.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(
                branch.Id.Value,
                null,
                (int)SaleStatus.OnHold,
                false,
                null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)],
                [new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 100m, null, CardBankId: bank.Id, CardCuotas: 1)],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Create.CardBankInvalid");
    }

    [Fact]
    public async Task Handle_ShouldRejectTransferPayment_WhenBankIsNotEnabledForTransfer()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);
        var bank = Bank.Create(companyId, "Banco transferencia", useForCard: true, useForTransfer: false, useForCheque: true);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bank> { bank });
        var handler = CreateHandler(companyId, branch, product, stock, bankRepository.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(
                branch.Id.Value,
                null,
                (int)SaleStatus.OnHold,
                false,
                null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)],
                [new CreateSalePaymentItemRequest((int)SalePaymentMethod.Transfer, 100m, null, TransferBankId: bank.Id)],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Create.TransferBankInvalid");
    }

    [Fact]
    public async Task Handle_ShouldRejectChequePayment_WhenBankIsNotEnabledForCheque()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var stock = BranchProductStock.Create(companyId, branch.Id, product.Id);
        stock.ApplyManualEntry(10);
        var bank = Bank.Create(companyId, "Banco cheque", useForCard: true, useForTransfer: true, useForCheque: false);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bank> { bank });
        var handler = CreateHandler(companyId, branch, product, stock, bankRepository.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(
                branch.Id.Value,
                null,
                (int)SaleStatus.OnHold,
                false,
                null,
                [new CreateSaleDetailItemRequest(product.Id.Value, 1)],
                [new CreateSalePaymentItemRequest(
                    (int)SalePaymentMethod.Check,
                    100m,
                    null,
                    Cheque: new CreateSalePaymentChequeData(
                        "000123",
                        bank.Id,
                        "Juan Perez",
                        "20123456789",
                        100m,
                        DateTime.UtcNow.Date,
                        DateTime.UtcNow.Date.AddDays(30),
                        null))],
                []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Sales.Create.ChequeBankInvalid");
    }

    private static CreateSaleHandler CreateHandler(
        CompanyId companyId,
        Branch branch,
        Product product,
        BranchProductStock stock,
        IBankRepository bankRepository)
    {
        var currentUserService = new Mock<ICurrentUserService>();
        var branchRepository = new Mock<IBranchRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        branchRepository
            .Setup(repository => repository.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        productRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        branchProductStockRepository
            .Setup(repository => repository.GetOrCreateAsync(branch.Id, product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);

        saleRepository
            .Setup(repository => repository.CountByBranchAsync(branch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        saleRepository
            .Setup(repository => repository.AddAsync(It.IsAny<eiti.Domain.Sales.Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new CreateSaleHandler(
            currentUserService.Object,
            branchRepository.Object,
            customerRepository.Object,
            productRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            saleRepository.Object,
            new Mock<ICashDrawerRepository>().Object,
            cashSessionRepository.Object,
            new Mock<IAddressRepository>().Object,
            bankRepository,
            new Mock<IChequeRepository>().Object,
            unitOfWork.Object);
    }
}
