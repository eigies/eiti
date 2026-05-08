using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Products.Commands.CreateProduct;
using eiti.Application.Features.Products.Commands.DeleteProduct;
using eiti.Application.Features.Products.Commands.ImportProducts;
using eiti.Application.Features.Products.Queries.ListPagedProducts;
using eiti.Application.Features.Products.Queries.ListProducts;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class ProductHandlersTests
{
    [Fact]
    public async Task CreateProduct_ShouldPersistProductForCurrentCompany()
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.NameExistsAsync(
                companyId,
                "Notebook",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateProductHandler(
            currentUserService.Object,
            productRepository.Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CreateProductCommand("NOTE-001", "NOTEBOOK-001", "Contoso", "Notebook", "Office device", 99.50m, null, 60m, 9.95m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Notebook");

        productRepository.Verify(repository => repository.AddAsync(
            It.Is<Product>(product =>
                product.CompanyId == companyId &&
                product.Brand == "Contoso" &&
                product.Name == "Notebook" &&
                product.Price == 99.50m &&
                product.AllowsManualValueInSale == false &&
                product.CostPrice == 60m &&
                product.UnitPrice == 9.95m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_ShouldAllowZeroPublicPrice_WhenManualValueInSaleIsEnabled()
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.NameExistsAsync(
                companyId,
                "Usado recibido",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        productRepository
            .Setup(repository => repository.CodeExistsAsync(
                companyId,
                "TRADE-001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        productRepository
            .Setup(repository => repository.SkuExistsAsync(
                companyId,
                "TRADE-001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateProductHandler(
            currentUserService.Object,
            productRepository.Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CreateProductCommand("TRADE-001", "TRADE-001", "Generico", "Usado recibido", null, 0m, null, 0m, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PublicPrice.Should().Be(0m);
        result.Value.AllowsManualValueInSale.Should().BeTrue();

        productRepository.Verify(repository => repository.AddAsync(
            It.Is<Product>(product =>
                product.CompanyId == companyId &&
                product.Name == "Usado recibido" &&
                product.Price == 0m &&
                product.AllowsManualValueInSale),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_ShouldRejectZeroPublicPrice_WhenManualValueInSaleIsDisabled()
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.NameExistsAsync(
                companyId,
                "Producto invalido",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        productRepository
            .Setup(repository => repository.CodeExistsAsync(
                companyId,
                "BAD-001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        productRepository
            .Setup(repository => repository.SkuExistsAsync(
                companyId,
                "BAD-001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateProductHandler(
            currentUserService.Object,
            productRepository.Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new CreateProductCommand("BAD-001", "BAD-001", "Generico", "Producto invalido", null, 0m, null, 0m, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Products.Create.PublicPriceMustBePositive");
    }

    [Fact]
    public async Task ImportProducts_ShouldCreateNewProducts()
    {
        var companyId = CompanyId.New();
        var products = new List<Product>();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        productRepository
            .Setup(repository => repository.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => products.Add(product))
            .Returns(Task.CompletedTask);

        companyOnboardingRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyOnboarding?)null);

        var handler = new ImportProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            new Mock<IBranchRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object,
            new Mock<IStockMovementRepository>().Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new ImportProductsCommand([
                new ImportProductRowRequest("BAT-001", "BAT-001", "Contoso", "Bateria", null, 100m, 70m, 10m, false, 5m, null, null)
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(1);
        result.Value.UpdatedCount.Should().Be(0);
        result.Value.ErrorCount.Should().Be(0);
        result.Value.Rows.Should().ContainSingle(row => row.Action == "created" && row.Code == "BAT-001");
        products.Should().ContainSingle(product => product.Code == "BAT-001" && product.Price == 100m);
        unitOfWork.Verify(workflow => workflow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportProducts_ShouldUpdateExistingProduct_WhenCodeMatches()
    {
        var companyId = CompanyId.New();
        var existingProduct = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 100m, 70m, 10m);
        var products = new List<Product> { existingProduct };

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var handler = new ImportProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            new Mock<IBranchRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object,
            new Mock<IStockMovementRepository>().Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new ImportProductsCommand([
                new ImportProductRowRequest("BAT-001", "BAT-002", "Acme", "Bateria Premium", "Nueva", 140m, 80m, 12m, false, 8m, null, null)
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(0);
        result.Value.UpdatedCount.Should().Be(1);
        result.Value.ErrorCount.Should().Be(0);
        existingProduct.Sku.Should().Be("BAT-002");
        existingProduct.Brand.Should().Be("Acme");
        existingProduct.Name.Should().Be("Bateria Premium");
        existingProduct.Price.Should().Be(140m);
    }

    [Fact]
    public async Task ImportProducts_ShouldReturnPartialResults_ForMixedRows()
    {
        var companyId = CompanyId.New();
        var existingProduct = Product.Create(companyId, "USED-001", "USED-001", "Contoso", "Usado", null, 0m, 0m, null, true);
        var skuOwner = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 100m, 70m, 10m);
        var products = new List<Product> { existingProduct, skuOwner };

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        productRepository
            .Setup(repository => repository.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => products.Add(product))
            .Returns(Task.CompletedTask);

        companyOnboardingRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyOnboarding?)null);

        var handler = new ImportProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            new Mock<IBranchRepository>().Object,
            new Mock<IBranchProductStockRepository>().Object,
            new Mock<IStockMovementRepository>().Object,
            companyOnboardingRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new ImportProductsCommand([
                new ImportProductRowRequest("NEW-001", "NEW-001", "Acme", "Nuevo", null, 120m, 60m, null, false, null, null, null),
                new ImportProductRowRequest("DUP-001", "DUP-001", "Acme", "Duplicado A", null, 120m, 60m, null, false, null, null, null),
                new ImportProductRowRequest("DUP-001", "DUP-002", "Acme", "Duplicado B", null, 130m, 60m, null, false, null, null, null),
                new ImportProductRowRequest("SKU-CLASH", "BAT-001", "Acme", "Choque SKU", null, 140m, 70m, null, false, null, null, null),
                new ImportProductRowRequest("USED-001", "USED-001", "Contoso", "Usado", null, 0m, 0m, null, true, null, null, null),
                new ImportProductRowRequest("BAD-001", "BAD-001", "Acme", "Precio invalido", null, 0m, 50m, null, false, null, null, null)
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(6);
        result.Value.CreatedCount.Should().Be(1);
        result.Value.UpdatedCount.Should().Be(1);
        result.Value.ErrorCount.Should().Be(4);
        result.Value.Rows.Should().Contain(row => row.RowNumber == 3 && row.Action == "error");
        result.Value.Rows.Should().Contain(row => row.RowNumber == 4 && row.Action == "error");
        result.Value.Rows.Should().Contain(row => row.RowNumber == 5 && row.Action == "error");
        result.Value.Rows.Should().Contain(row => row.RowNumber == 7 && row.Action == "error");
        result.Value.Rows.Should().Contain(row => row.RowNumber == 6 && row.Action == "updated");
        products.Should().Contain(product => product.Code == "NEW-001");
    }

    [Fact]
    public async Task ListProducts_ShouldReturnOnlyProductsForCurrentCompany()
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                Product.Create(companyId, "LAP-001", "LAPTOP-001", "Contoso", "Laptop", "Portable", 1200m, 900m, 100m),
                Product.Create(companyId, "MOU-001", "MOUSE-001", "Contoso", "Mouse", null, 25m, 10m, null)
            });

        branchProductStockRepository
            .Setup(repository => repository.ListByCompanyAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new ListProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            branchProductStockRepository.Object);

        var result = await handler.Handle(new ListProductsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(product => product.Name)
            .Should()
            .Contain(new[] { "Laptop", "Mouse" });
    }

    [Fact]
    public async Task DeleteProduct_ShouldRemoveProductWhenItIsNotReferenced()
    {
        var companyId = CompanyId.New();
        var product = Product.Create(companyId, "KEY-001", "KEYBOARD-001", "Contoso", "Keyboard", null, 50m, 30m, null);

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByIdAsync(
                product.Id,
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        productRepository
            .Setup(repository => repository.IsReferencedAsync(
                product.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new DeleteProductHandler(
            currentUserService.Object,
            productRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new DeleteProductCommand(product.Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        productRepository.Verify(repository => repository.Remove(product), Times.Once);
        unitOfWork.Verify(workflow => workflow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListPagedProducts_ShouldReturnRequestedPage()
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        var productRepository = new Mock<IProductRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                Product.Create(companyId, "LAP-001", "LAPTOP-001", "Contoso", "Laptop", "Portable", 1200m, 900m, 100m),
                Product.Create(companyId, "MOU-001", "MOUSE-001", "Contoso", "Mouse", null, 25m, 10m, null),
                Product.Create(companyId, "KEY-001", "KEYBOARD-001", "Contoso", "Keyboard", null, 50m, 20m, null)
            });

        branchProductStockRepository
            .Setup(repository => repository.ListByCompanyAsync(
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new ListPagedProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            branchProductStockRepository.Object);

        var result = await handler.Handle(new ListPagedProductsQuery(2, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.TotalPages.Should().Be(2);
        result.Value.Items.Should().HaveCount(1);
    }
}
