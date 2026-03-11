using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Products.Commands.CreateProduct;
using eiti.Application.Features.Products.Commands.DeleteProduct;
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
                product.CostPrice == 60m &&
                product.UnitPrice == 9.95m),
            It.IsAny<CancellationToken>()), Times.Once);
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
