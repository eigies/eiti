using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
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
            .Setup(repository => repository.GetByIdAsync(saleProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saleProduct);

        productRepository
            .Setup(repository => repository.GetByIdAsync(tradeInProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tradeInProduct);

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
            cashSessionRepository.Object,
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
            .Setup(repository => repository.GetByIdAsync(saleProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saleProduct);

        productRepository
            .Setup(repository => repository.GetByIdAsync(regularProduct.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regularProduct);

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
            cashSessionRepository.Object,
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
}
