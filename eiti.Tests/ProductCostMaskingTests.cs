using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Products.Commands.UpdateProduct;
using eiti.Application.Features.Products.Queries.ListPagedProducts;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Regresión del borrado masivo de costos de 2026-09-21 (marca MOURA, Baterías Soler).
///
/// La cadena que lo produjo: la lista de productos enmascaraba el costo con 0 para quien
/// no tiene products.view_cost, la grilla de edición masiva cargaba ese 0 en el formulario,
/// y al guardar lo mandaba de vuelta en UpdateProductCommand.CostPrice (decimal no nullable),
/// que lo persistía pisando el costo real. Los tres eslabones se cubren acá.
/// </summary>
public sealed class ProductCostMaskingTests
{
    private static Product CreateProductWithCost(CompanyId companyId, decimal costPrice) =>
        Product.Create(companyId, "22GD", "22GD", "MOURA", "BATERIA MOURA 12 x 65 REFORZADA 22GD", null, 169900m, costPrice, null);

    private static UpdateProductCommand BuildCommand(Product product, decimal? costPrice) =>
        new(
            product.Id.Value,
            product.Code,
            product.Sku,
            product.Brand,
            product.Name,
            product.Description,
            Price: null,
            PublicPrice: product.Price,
            CostPrice: costPrice,
            UnitPrice: null);

    private static UpdateProductHandler BuildHandler(CompanyId companyId, Product product)
    {
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByIdAsync(product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        return new UpdateProductHandler(
            currentUserService.Object,
            productRepository.Object,
            new Mock<IProductCategoryRepository>().Object,
            new Mock<IUnitOfWork>().Object);
    }

    [Fact]
    public async Task UpdateProduct_ShouldKeepCurrentCost_WhenCostPriceIsNull()
    {
        var companyId = CompanyId.New();
        var product = CreateProductWithCost(companyId, 98332.16m);
        var handler = BuildHandler(companyId, product);

        var result = await handler.Handle(BuildCommand(product, costPrice: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.CostPrice.Should().Be(98332.16m, "null significa 'no tocar el costo', no 'costo cero'");
        result.Value.CostPrice.Should().Be(98332.16m);
    }

    [Fact]
    public async Task UpdateProduct_ShouldSetCostToZero_WhenCostPriceIsExplicitZero()
    {
        var companyId = CompanyId.New();
        var product = CreateProductWithCost(companyId, 98332.16m);
        var handler = BuildHandler(companyId, product);

        var result = await handler.Handle(BuildCommand(product, costPrice: 0m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.CostPrice.Should().Be(0m, "un 0 explícito sigue siendo un costo cero válido");
    }

    [Fact]
    public async Task UpdateProduct_ShouldUpdateCost_WhenCostPriceHasValue()
    {
        var companyId = CompanyId.New();
        var product = CreateProductWithCost(companyId, 98332.16m);
        var handler = BuildHandler(companyId, product);

        var result = await handler.Handle(BuildCommand(product, costPrice: 105000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.CostPrice.Should().Be(105000m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListPagedProducts_ShouldMaskCostWithNull_WhenUserCannotViewCost(bool canViewCost)
    {
        var companyId = CompanyId.New();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.Setup(service => service.HasPermission(PermissionCodes.ProductsViewCost)).Returns(canViewCost);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { CreateProductWithCost(companyId, 98332.16m) });

        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        branchProductStockRepository
            .Setup(repository => repository.ListByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new ListPagedProductsHandler(
            currentUserService.Object,
            productRepository.Object,
            new Mock<IProductCategoryRepository>().Object,
            branchProductStockRepository.Object);

        var result = await handler.Handle(new ListPagedProductsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].CostPrice
            .Should().Be(canViewCost ? 98332.16m : null, "enmascarar con 0 hacía que ese 0 volviera como costo real");
    }
}
