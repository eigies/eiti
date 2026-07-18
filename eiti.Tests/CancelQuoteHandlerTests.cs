using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Quotes.Commands.CancelQuote;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CancelQuoteHandlerTests
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
    public async Task Handle_ShouldCancelQuote_WhenQuoteIsPending()
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

        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CancelQuoteHandler(
            currentUserService.Object,
            quoteRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(new CancelQuoteCommand(quote.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        quote.Status.Should().Be(QuoteStatus.Cancelled);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        var handler = new CancelQuoteHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new CancelQuoteCommand(quoteId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CancelQuoteErrors.QuoteNotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenQuoteIsAlreadyCancelled()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);
        var quote = CreatePendingQuote(companyId, branch, product);
        quote.Cancel();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.GetByIdAsync(quote.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CancelQuoteHandler(
            currentUserService.Object,
            quoteRepository.Object,
            unitOfWork.Object);

        var result = await handler.Handle(new CancelQuoteCommand(quote.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
