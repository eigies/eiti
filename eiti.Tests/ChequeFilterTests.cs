using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Cheques.Queries.ListCheques;
using eiti.Domain.Banks;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class ChequeFilterTests
{
    [Fact]
    public void ChequeFilters_ShouldCarryNumeroFilter()
    {
        var filters = new ChequeFilters(null, null, null, null, "123");

        filters.Numero.Should().Be("123");
    }

    [Fact]
    public async Task ListCheques_ShouldResolveCcChequeSaleCode_FromCustomerPaymentId()
    {
        var companyId = CompanyId.New();
        var customerPaymentId = Guid.NewGuid();
        var saleCode = "SC-001";
        var bank = Bank.Create(companyId, "BBVA");
        var cheque = Cheque.CreateForCcPayment(
            companyId,
            customerPaymentId,
            bank.Id,
            "123441",
            "Agustin Testa",
            "20123456789",
            5000m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(30),
            null);
        var currentUserService = new Mock<ICurrentUserService>();
        var chequeRepository = new Mock<IChequeRepository>();
        var bankRepository = new Mock<IBankRepository>();
        var saleRepository = new Mock<ISaleRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        chequeRepository
            .Setup(repository => repository.ListAsync(It.IsAny<ChequeFilters>(), companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([cheque]);
        bankRepository
            .Setup(repository => repository.ListAsync(false, companyId, It.IsAny<CancellationToken>(), BankUsage.All))
            .ReturnsAsync([bank]);
        saleRepository
            .Setup(repository => repository.GetCodesByCustomerPaymentIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Single() == customerPaymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string?> { [customerPaymentId] = saleCode });

        var handler = new ListChequesHandler(
            currentUserService.Object,
            chequeRepository.Object,
            bankRepository.Object,
            saleRepository.Object);

        var result = await handler.Handle(
            new ListChequesQuery(null, null, null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.SaleCode.Should().Be(saleCode);
    }
}
