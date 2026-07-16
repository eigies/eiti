using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Quotes.Commands.CreateQuote;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CreateQuoteHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateQuote_ForProspect_WhenNoCustomerId()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdAsync(product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var quoteRepository = new Mock<IQuoteRepository>();
        Quote? persistedQuote = null;
        quoteRepository
            .Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((quote, _) => persistedQuote = quote)
            .Returns(Task.CompletedTask);
        quoteRepository
            .Setup(r => r.CountByBranchAsync(branch.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new CreateQuoteHandler(
            currentUserService.Object,
            branchRepository.Object,
            new Mock<ICustomerRepository>().Object,
            productRepository.Object,
            quoteRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateQuoteCommand(
                branch.Id.Value,
                CustomerId: null,
                ProspectName: "Juan Perez",
                ProspectContact: "1122334455",
                Details: [new CreateQuoteDetailItemRequest(product.Id.Value, 2, 150m)],
                GeneralDiscountPercent: 0,
                ExpiresAt: DateTime.UtcNow.AddDays(7)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(300m);
        result.Value.ProspectName.Should().Be("Juan Perez");
        persistedQuote.Should().NotBeNull();
        persistedQuote!.Status.Should().Be(QuoteStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCustomerIdProvidedButNotFound()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var customerId = Guid.NewGuid();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var customerRepository = new Mock<ICustomerRepository>();
        customerRepository
            .Setup(r => r.GetByIdAsync(new CustomerId(customerId), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var handler = new CreateQuoteHandler(
            currentUserService.Object,
            branchRepository.Object,
            customerRepository.Object,
            new Mock<IProductRepository>().Object,
            new Mock<IQuoteRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateQuoteCommand(
                branch.Id.Value,
                CustomerId: customerId,
                ProspectName: null,
                ProspectContact: null,
                Details: [new CreateQuoteDetailItemRequest(Guid.NewGuid(), 2, 150m)],
                GeneralDiscountPercent: 0,
                ExpiresAt: DateTime.UtcNow.AddDays(7)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CreateQuoteErrors.CustomerNotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNeitherCustomerNorProspectProvided()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var handler = new CreateQuoteHandler(
            currentUserService.Object,
            branchRepository.Object,
            new Mock<ICustomerRepository>().Object,
            new Mock<IProductRepository>().Object,
            new Mock<IQuoteRepository>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateQuoteCommand(
                branch.Id.Value,
                CustomerId: null,
                ProspectName: null,
                ProspectContact: null,
                Details: [new CreateQuoteDetailItemRequest(Guid.NewGuid(), 2, 150m)],
                GeneralDiscountPercent: 0,
                ExpiresAt: DateTime.UtcNow.AddDays(7)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CreateQuoteErrors.InvalidCustomerOrProspect);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenProductNotFound()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var productId = Guid.NewGuid();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var branchRepository = new Mock<IBranchRepository>();
        branchRepository
            .Setup(r => r.GetByIdAsync(branch.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdAsync(new ProductId(productId), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var quoteRepository = new Mock<IQuoteRepository>();

        var handler = new CreateQuoteHandler(
            currentUserService.Object,
            branchRepository.Object,
            new Mock<ICustomerRepository>().Object,
            productRepository.Object,
            quoteRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateQuoteCommand(
                branch.Id.Value,
                CustomerId: null,
                ProspectName: "Juan Perez",
                ProspectContact: "1122334455",
                Details: [new CreateQuoteDetailItemRequest(productId, 2, 150m)],
                GeneralDiscountPercent: 0,
                ExpiresAt: DateTime.UtcNow.AddDays(7)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CreateQuoteErrors.ProductNotFound);
        quoteRepository.Verify(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
