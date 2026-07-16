using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Quotes.Queries.GetQuoteById;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GetQuoteByIdHandlerTests
{
    private static Quote CreatePendingQuote(CompanyId companyId, Branch branch, Product product)
    {
        return Quote.Create(
            companyId,
            branch.Id,
            customerId: null,
            prospectName: "Juan Perez",
            prospectContact: "1122334455",
            details: [QuoteDetail.Create(product.Id, 2, 150m)],
            generalDiscountPercent: 0,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByUserId: Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ShouldReturnQuoteDetail_WhenQuoteFound()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var quote = CreatePendingQuote(companyId, branch, product);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdsAsync(
                It.Is<IEnumerable<ProductId>>(ids => ids.Contains(product.Id)),
                companyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var handler = new GetQuoteByIdHandler(
            currentUserService.Object,
            quoteRepository.Object,
            branchRepository.Object,
            new Mock<ICustomerRepository>().Object,
            productRepository.Object);

        var result = await handler.Handle(new GetQuoteByIdQuery(quote.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(quote.Id.Value);
        result.Value.TotalAmount.Should().Be(300m);
        result.Value.BranchName.Should().Be("Sucursal Centro");
        result.Value.ProspectName.Should().Be("Juan Perez");
        result.Value.Details.Should().ContainSingle();
        result.Value.Details.Single().ProductName.Should().Be("Bateria nueva");
        result.Value.Details.Single().ProductBrand.Should().Be("Contoso");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenQuoteNotFound()
    {
        var companyId = CompanyId.New();
        var quoteId = Guid.NewGuid();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(new QuoteId(quoteId), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);

        var handler = new GetQuoteByIdHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<IBranchRepository>().Object,
            new Mock<ICustomerRepository>().Object,
            new Mock<IProductRepository>().Object);

        var result = await handler.Handle(new GetQuoteByIdQuery(quoteId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GetQuoteByIdErrors.QuoteNotFound);
    }
}
