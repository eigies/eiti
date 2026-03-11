using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Commands.DeleteSale;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class SalesHandlersTests
{
    [Fact]
    public async Task DeleteSale_ShouldRemoveSale_WhenSaleIsCancelled()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var product = Product.Create(companyId, "MON-001", "MONITOR-001", "Contoso", "Monitor", null, 200m, 150m, null);
        var sale = Sale.Create(
            companyId,
            branchId,
            null,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)]);

        sale.Update(
            null,
            SaleStatus.Cancel,
            false,
            [SaleDetail.Create(product.Id, 1, product.Price)]);

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        saleRepository
            .Setup(repository => repository.GetByIdAsync(
                sale.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        var handler = new DeleteSaleHandler(
            currentUserService.Object,
            saleRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new DeleteSaleCommand(sale.Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saleRepository.Verify(repository => repository.Remove(sale), Times.Once);
        unitOfWork.Verify(workflow => workflow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
