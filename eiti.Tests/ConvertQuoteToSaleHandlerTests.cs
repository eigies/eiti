using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

public sealed class ConvertQuoteToSaleHandlerTests
{
    private static Quote BuildQuote(DateTime expiresAt, CompanyId companyId, BranchId branchId, DateTime? createdAt = null)
    {
        return Quote.Create(
            companyId, branchId, CustomerId.New(), null, null,
            new[] { QuoteDetail.Create(ProductId.New(), 1, 100m) },
            0, expiresAt, Guid.NewGuid(), createdAt: createdAt);
    }

    [Fact]
    public async Task Handle_ShouldMarkConverted_WhenPendingAndNotExpired()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var quote = BuildQuote(DateTime.UtcNow.AddDays(7), companyId, branchId);
        var saleId = Guid.NewGuid();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CreateCcSaleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateCcSaleResponse>.Success(new CreateCcSaleResponse(
                saleId, "SC-001", branchId.Value, Guid.NewGuid(), "Juan Perez",
                1, "OnHold", 0, 100m, 100m, null, true, DateTime.UtcNow,
                0, 0, [], 0, 100m, [])));

        var handler = new ConvertQuoteToSaleHandler(
            currentUserService.Object,
            quoteRepository.Object,
            sender.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new ConvertQuoteToSaleCommand(
                quote.Id.Value, branchId.Value, Guid.NewGuid(),
                [new CreateSaleDetailItemRequest(Guid.NewGuid(), 1, null, 0)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(saleId);
        quote.Status.Should().Be(QuoteStatus.Converted);
        quote.ConvertedSaleId.Should().Be(saleId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenQuoteExpired()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var quote = BuildQuote(
            DateTime.UtcNow.AddSeconds(-1), companyId, branchId, createdAt: DateTime.UtcNow.AddDays(-1));

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var handler = new ConvertQuoteToSaleHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<ISender>().Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new ConvertQuoteToSaleCommand(
                quote.Id.Value, branchId.Value, Guid.NewGuid(),
                [new CreateSaleDetailItemRequest(Guid.NewGuid(), 1, null, 0)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Quotes.Convert.Expired");
        quote.Status.Should().Be(QuoteStatus.Pending);
    }
}
