using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Customers.Queries.GetCustomerAccount;
using eiti.Application.Features.Customers.Queries.ListCustomerAccounts;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CustomerAccountsHandlerTests
{
    [Fact]
    public async Task Handle_ShouldFindCustomerAccount_WhenSearchMatchesDocumentNumber()
    {
        var companyId = CompanyId.New();
        var customer = Customer.Create(
            companyId,
            "Juan",
            "Perez",
            null,
            phone: "1155550000",
            documentType: DocumentType.Dni,
            documentNumber: "42123456",
            taxId: null);

        var currentUserService = new Mock<ICurrentUserService>();
        var customerRepository = new Mock<ICustomerRepository>();
        var saleRepository = new Mock<ISaleRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        saleRepository
            .Setup(repository => repository.GetPendingCcTotalsByCustomerAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal>
            {
                [customer.Id.Value] = 1500m
            });

        saleRepository
            .Setup(repository => repository.ListCcSalesByCompanyAsync(companyId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        customerRepository
            .Setup(repository => repository.ListWithPositiveCreditAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        customerRepository
            .Setup(repository => repository.ListByIdsAsync(
                companyId,
                It.Is<IEnumerable<CustomerId>>(ids => ids.Any(id => id == customer.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);

        var handler = new ListCustomerAccountsHandler(
            currentUserService.Object,
            customerRepository.Object,
            saleRepository.Object);

        var result = await handler.Handle(new ListCustomerAccountsQuery("42"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].CustomerId.Should().Be(customer.Id.Value);
    }

    [Fact]
    public async Task Handle_ShouldFindCustomerAccount_WhenSearchMatchesFormattedDocumentNumber()
    {
        var companyId = CompanyId.New();
        var customer = Customer.Create(
            companyId,
            "Ana",
            "Gomez",
            null,
            phone: null,
            documentType: DocumentType.Dni,
            documentNumber: "42.932.766",
            taxId: null);

        var currentUserService = new Mock<ICurrentUserService>();
        var customerRepository = new Mock<ICustomerRepository>();
        var saleRepository = new Mock<ISaleRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        saleRepository
            .Setup(repository => repository.GetPendingCcTotalsByCustomerAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal>
            {
                [customer.Id.Value] = 1500m
            });

        saleRepository
            .Setup(repository => repository.ListCcSalesByCompanyAsync(companyId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        customerRepository
            .Setup(repository => repository.ListWithPositiveCreditAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        customerRepository
            .Setup(repository => repository.ListByIdsAsync(
                companyId,
                It.Is<IEnumerable<CustomerId>>(ids => ids.Any(id => id == customer.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);

        var handler = new ListCustomerAccountsHandler(
            currentUserService.Object,
            customerRepository.Object,
            saleRepository.Object);

        var result = await handler.Handle(new ListCustomerAccountsQuery("42932766"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].CustomerId.Should().Be(customer.Id.Value);
    }

    [Fact]
    public async Task GetCustomerAccount_ShouldClampPendingBalanceAndExcludeCancelledPayments()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 4000m, 2500m, null);
        var customer = Customer.Create(
            companyId,
            "Testa",
            "Agustin",
            null,
            documentType: DocumentType.Dni,
            documentNumber: "42932766");
        customer.AddCredit(2000m);

        var sale = Sale.CreateCc(
            companyId,
            branchId,
            customer.Id,
            [SaleDetail.Create(product.Id, 1, 4000m)],
            code: "SUCU-123-088");
        sale.AddCcPayment(SalePaymentMethod.Cash, 6000m, DateTime.UtcNow, null, allowOverpayment: true);

        var cancelledPayment = CustomerPayment.Create(
            companyId.Value,
            customer.Id.Value,
            branchId.Value,
            SalePaymentMethod.Cash,
            1000m,
            DateTime.UtcNow,
            null,
            null,
            Guid.NewGuid());
        cancelledPayment.Cancel();

        var currentUserService = new Mock<ICurrentUserService>();
        var customerRepository = new Mock<ICustomerRepository>();
        var customerPaymentRepository = new Mock<ICustomerPaymentRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var chequeRepository = new Mock<IChequeRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        customerRepository
            .Setup(repository => repository.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        saleRepository
            .Setup(repository => repository.ListCcSalesByCustomerAsync(companyId, customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);

        customerPaymentRepository
            .Setup(repository => repository.ListByCustomerAsync(companyId.Value, customer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([cancelledPayment]);

        var handler = new GetCustomerAccountHandler(
            currentUserService.Object,
            customerRepository.Object,
            customerPaymentRepository.Object,
            saleRepository.Object,
            chequeRepository.Object);

        var result = await handler.Handle(new GetCustomerAccountQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeudaTotal.Should().Be(4000m);
        result.Value.CobradoTotal.Should().Be(6000m);
        result.Value.SaldoPendiente.Should().Be(0m);
        result.Value.SaldoAFavor.Should().Be(2000m);
    }

    [Fact]
    public async Task ListCustomerAccounts_ShouldIncludeCustomerWithCcHistoryEvenWhenBalanceIsZero()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 4000m, 2500m, null);
        var customer = Customer.Create(
            companyId,
            "Testa",
            "Agustin",
            null,
            documentType: DocumentType.Dni,
            documentNumber: "42932766");
        var sale = Sale.CreateCc(
            companyId,
            branchId,
            customer.Id,
            [SaleDetail.Create(product.Id, 1, 4000m)],
            code: "SUCU-123-108");
        sale.AddCcPayment(SalePaymentMethod.Cash, 4000m, DateTime.UtcNow, null);

        var currentUserService = new Mock<ICurrentUserService>();
        var customerRepository = new Mock<ICustomerRepository>();
        var saleRepository = new Mock<ISaleRepository>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);

        saleRepository
            .Setup(repository => repository.GetPendingCcTotalsByCustomerAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        saleRepository
            .Setup(repository => repository.ListCcSalesByCompanyAsync(companyId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);

        customerRepository
            .Setup(repository => repository.ListWithPositiveCreditAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        customerRepository
            .Setup(repository => repository.ListByIdsAsync(
                companyId,
                It.Is<IEnumerable<CustomerId>>(ids => ids.Any(id => id == customer.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);

        var handler = new ListCustomerAccountsHandler(
            currentUserService.Object,
            customerRepository.Object,
            saleRepository.Object);

        var result = await handler.Handle(new ListCustomerAccountsQuery("42932766"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].CustomerId.Should().Be(customer.Id.Value);
        result.Value.Items[0].SaldoPendiente.Should().Be(0m);
        result.Value.Items[0].SaldoAFavor.Should().Be(0m);
    }
}
