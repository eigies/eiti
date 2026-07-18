using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Quotes.Queries.ListQuotes;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Quotes;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class ListQuotesHandlerTests
{
    private static Quote CreatePendingQuote(
        CompanyId companyId, BranchId branchId, Product product, DateTime expiresAt, DateTime? createdAt = null)
    {
        return Quote.Create(
            companyId,
            branchId,
            customerId: null,
            prospectName: "Juan Perez",
            prospectContact: "1122334455",
            details: [QuoteDetail.Create(product.Id, 2, 150m)],
            generalDiscountPercent: 0,
            expiresAt: expiresAt,
            createdByUserId: Guid.NewGuid(),
            createdAt: createdAt);
    }

    [Fact]
    public async Task Handle_ShouldFilterQuotes_ToAllowedBranches_WhenCannotViewAllBranches()
    {
        var companyId = CompanyId.New();
        var allowedBranch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var disallowedBranch = Branch.Create(companyId, "Sucursal Norte", "SN", "Belgrano 456");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);

        var allowedQuote = CreatePendingQuote(companyId, allowedBranch.Id, product, DateTime.UtcNow.AddDays(7));
        var disallowedQuote = CreatePendingQuote(companyId, disallowedBranch.Id, product, DateTime.UtcNow.AddDays(7));

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());
        currentUserService.SetupGet(s => s.CanViewAllBranches).Returns(false);
        currentUserService.SetupGet(s => s.AllowedBranchIds).Returns([allowedBranch.Id.Value]);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.ListAsync(companyId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([allowedQuote, disallowedQuote]);

        var handler = new ListQuotesHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<ICustomerRepository>().Object);

        var result = await handler.Handle(
            new ListQuotesQuery(null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Id.Should().Be(allowedQuote.Id.Value);
        result.Value.Single().BranchId.Should().Be(allowedBranch.Id.Value);
    }

    [Fact]
    public async Task Handle_ShouldMarkIsExpired_OnlyForPendingQuotesPastExpiration()
    {
        var companyId = CompanyId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria nueva", null, 100m, 70m, null);

        // Quote.Create requires expiresAt > createdAt, so backdate createdAt to allow an
        // expiresAt that is already in the past relative to "now" (DateTime.UtcNow at assert time).
        var backdatedCreatedAt = DateTime.UtcNow.AddDays(-10);
        var pastExpiresAt = DateTime.UtcNow.AddDays(-1);

        var expiredPendingQuote = CreatePendingQuote(companyId, branch.Id, product, pastExpiresAt, backdatedCreatedAt);
        var futurePendingQuote = CreatePendingQuote(companyId, branch.Id, product, DateTime.UtcNow.AddDays(7));

        var cancelledExpiredQuote = CreatePendingQuote(companyId, branch.Id, product, pastExpiresAt, backdatedCreatedAt);
        cancelledExpiredQuote.Cancel();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(s => s.CompanyId).Returns(companyId);
        currentUserService.SetupGet(s => s.UserId).Returns(UserId.New());
        currentUserService.SetupGet(s => s.CanViewAllBranches).Returns(true);

        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(r => r.ListAsync(companyId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expiredPendingQuote, futurePendingQuote, cancelledExpiredQuote]);

        var handler = new ListQuotesHandler(
            currentUserService.Object,
            quoteRepository.Object,
            new Mock<ICustomerRepository>().Object);

        var result = await handler.Handle(
            new ListQuotesQuery(null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Single(q => q.Id == expiredPendingQuote.Id.Value).IsExpired.Should().BeTrue();
        result.Value.Single(q => q.Id == futurePendingQuote.Id.Value).IsExpired.Should().BeFalse();
        result.Value.Single(q => q.Id == cancelledExpiredQuote.Id.Value).IsExpired.Should().BeFalse();
    }
}
