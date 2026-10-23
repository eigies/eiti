using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Sales.Queries.ListSales;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

// El asistente busca una venta por su código ("SUCU-123-179") sin saber de qué fecha es.
public sealed class ListSalesByCodeTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly BranchId _branchId = BranchId.New();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ISaleRepository> _sales = new();

    public ListSalesByCodeTests()
    {
        _currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(service => service.CompanyId).Returns(_companyId);
        _currentUser.SetupGet(service => service.CanViewAllBranches).Returns(true);
    }

    [Fact]
    public async Task Handle_WithCode_FindsTheSaleOutsideTheDateRange()
    {
        var sale = NewSale("SUCU-123-179");
        _sales.Setup(repository => repository.ListByCodeAsync(_companyId, "SUCU-123-179", It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);

        var result = await Handler().Handle(
            new ListSalesQuery(new DateTime(2020, 1, 1), new DateTime(2020, 1, 2), null, false, "  SUCU-123-179 "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(item => item.Code).Should().Equal("SUCU-123-179");
        _sales.Verify(repository => repository.ListByCompanyAsync(
            It.IsAny<CompanyId>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCode_IncludesCuentaCorrienteSalesWithoutTheFlag()
    {
        var sale = Sale.CreateCc(_companyId, _branchId, CustomerId.New(), [SaleDetail.Create(ProductId.New(), 1, 1000m)], code: "MAIN-123");
        _sales.Setup(repository => repository.ListByCodeAsync(_companyId, "MAIN-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync([sale]);

        var result = await Handler().Handle(new ListSalesQuery(null, null, null, Code: "MAIN-123"), CancellationToken.None);

        result.Value.Should().ContainSingle().Which.IsCuentaCorriente.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithCode_OnlySearchesTheUsersCompany()
    {
        var otherCompany = CompanyId.New();
        _sales.Setup(repository => repository.ListByCodeAsync(otherCompany, "MAIN-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewSale("MAIN-1")]);
        _sales.Setup(repository => repository.ListByCodeAsync(_companyId, "MAIN-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Handler().Handle(new ListSalesQuery(null, null, null, Code: "MAIN-1"), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCode_KeepsTheBranchRestriction()
    {
        _currentUser.SetupGet(service => service.CanViewAllBranches).Returns(false);
        _currentUser.SetupGet(service => service.AllowedBranchIds).Returns([Guid.NewGuid()]);
        _sales.Setup(repository => repository.ListByCodeAsync(_companyId, "MAIN-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewSale("MAIN-2")]);

        var result = await Handler().Handle(new ListSalesQuery(null, null, null, Code: "MAIN-2"), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutCode_ListsByDateAsBefore()
    {
        _sales.Setup(repository => repository.ListByCompanyAsync(
                _companyId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewSale("MAIN-3")]);

        var result = await Handler().Handle(
            new ListSalesQuery(new DateTime(2026, 9, 28), new DateTime(2026, 9, 28), null), CancellationToken.None);

        result.Value.Select(item => item.Code).Should().Equal("MAIN-3");
        _sales.Verify(repository => repository.ListByCodeAsync(
            It.IsAny<CompanyId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Sale NewSale(string code) => Sale.Create(
        _companyId,
        _branchId,
        null,
        false,
        SaleStatus.OnHold,
        [SaleDetail.Create(ProductId.New(), 1, 1000m)],
        [SalePayment.Create(SalePaymentMethod.Cash, 1000m, null)],
        allowOverpayment: true,
        code: code);

    private ListSalesHandler Handler()
    {
        var transport = new Mock<ISaleTransportAssignmentRepository>();
        transport.Setup(repository => repository.ListBySaleIdsAsync(It.IsAny<IReadOnlyList<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var fiscal = new Mock<ISaleFiscalDocumentRepository>();
        fiscal.Setup(repository => repository.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var products = new Mock<IProductRepository>();
        products.Setup(repository => repository.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(repository => repository.ListByIdsAsync(It.IsAny<CompanyId>(), It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new ListSalesHandler(
            _currentUser.Object,
            _sales.Object,
            products.Object,
            customers.Object,
            transport.Object,
            new Mock<IEmployeeRepository>().Object,
            new Mock<IVehicleRepository>().Object,
            new Mock<IAddressRepository>().Object,
            fiscal.Object);
    }
}
