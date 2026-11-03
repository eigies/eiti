using eiti.Api.Controllers;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Reports.Queries.CashMovementsReport;
using eiti.Application.Features.Reports.Queries.CustomerDebtors;
using eiti.Application.Features.Reports.Queries.DailySalesControl;
using eiti.Application.Features.Reports.Queries.DiscountsReport;
using eiti.Application.Features.Reports.Queries.PaymentMethodsReport;
using eiti.Application.Features.Reports.Queries.StockMatrix;
using eiti.Application.Features.Reports.Queries.TradeInsByBranch;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace eiti.Tests;

// Los reportes comparten Sucursal, Cliente y Tipo de venta en todos los casos donde esa dimensión existe.
public sealed class ReportFiltersTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31);

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _mataderos;
    private readonly Branch _sanCristobal;
    private readonly Customer _ana;
    private readonly Customer _beto;
    private readonly Product _battery;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICustomerPaymentRepository> _customerPayments = new();
    private readonly Mock<IBankRepository> _banks = new();
    private readonly Mock<ICashSessionRepository> _cashSessions = new();
    private readonly Mock<IBranchProductStockRepository> _stocks = new();

    public ReportFiltersTests()
    {
        _mataderos = Branch.Create(_companyId, "Mataderos", "MAT", null);
        _sanCristobal = Branch.Create(_companyId, "San Cristobal", "SC", null);
        _ana = Customer.Create(_companyId, "Ana", "Perez", null);
        _beto = Customer.Create(_companyId, "Beto", "Ruiz", null);
        _battery = Product.Create(_companyId, "BAT-100", "BAT-100", "Moura", "M100", null, 200m, 120m, null);

        _currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(s => s.CompanyId).Returns(_companyId);
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        _branches.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_mataderos, _sanCristobal]);
        _products.Setup(r => r.GetByCompanyIdAsync(_companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_battery]);
        _customers.Setup(r => r.GetByIdAsync(It.IsAny<CustomerId>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId id, CompanyId _, CancellationToken _) => id == _ana.Id ? _ana : _beto);
        _customers.Setup(r => r.ListByIdsAsync(_companyId, It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_ana, _beto]);
        _cashSessions.Setup(r => r.ListMovementsByCompanyAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<CashMovementType>>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task DailyControl_FiltersByBranch_AndTheTotalsFollow()
    {
        StoreDailySales(Retail(_mataderos, _ana, 100m, "M-1"), Retail(_sanCristobal, _ana, 200m, "S-1"));

        var result = await DailyHandler().Handle(
            new DailySalesControlQuery(From, To, BranchId: _mataderos.Id.Value), CancellationToken.None);

        result.Value.Rows.Select(row => row.Code).Should().Equal("M-1");
        result.Value.Totals.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task DailyControl_FiltersByCustomer()
    {
        StoreDailySales(Retail(_mataderos, _ana, 100m, "M-1"), Retail(_mataderos, _beto, 200m, "M-2"));

        var result = await DailyHandler().Handle(
            new DailySalesControlQuery(From, To, CustomerId: _beto.Id.Value), CancellationToken.None);

        result.Value.Rows.Select(row => row.Code).Should().Equal("M-2");
    }

    [Theory]
    [InlineData("wholesale", new[] { "CC-1" })]
    [InlineData("retail", new[] { "R-1" })]
    [InlineData("all", new[] { "CC-1", "R-1" })]
    [InlineData(null, new[] { "CC-1", "R-1" })]
    public async Task DailyControl_FiltersBySaleType(string? saleType, string[] expected)
    {
        StoreDailySales(Retail(_mataderos, _ana, 100m, "R-1"), Wholesale(_mataderos, _ana, 200m, "CC-1"));

        var result = await DailyHandler().Handle(
            new DailySalesControlQuery(From, To, SaleType: saleType), CancellationToken.None);

        result.Value.Rows.Select(row => row.Code).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task DailyControl_DoesNotOpenABranchTheUserCannotSee()
    {
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(false);
        _currentUser.SetupGet(s => s.AllowedBranchIds).Returns([_mataderos.Id.Value]);
        StoreDailySales(Retail(_sanCristobal, _ana, 200m, "S-1"));

        var result = await DailyHandler().Handle(
            new DailySalesControlQuery(From, To, BranchId: _sanCristobal.Id.Value), CancellationToken.None);

        result.Value.Rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData("wholesale", 2)]
    [InlineData("retail", 1)]
    [InlineData(null, 3)]
    public async Task TradeIns_FiltersBySaleType(string? saleType, int expectedUnits)
    {
        var used = Product.Create(_companyId, "USADA", "USADA", "Generica", "Usada", null, 0m, 0m, null, true);
        var retail = Sale.Create(_companyId, _mataderos.Id, _ana.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, 100m)], tradeIns: [SaleTradeIn.Create(used.Id, 1, 10m)], code: "R-1");
        var wholesale = Sale.CreateCc(_companyId, _mataderos.Id, _ana.Id,
            [SaleDetail.Create(_battery.Id, 1, 100m)], tradeIns: [SaleTradeIn.Create(used.Id, 2, 10m)], code: "CC-1");
        _sales.Setup(r => r.ListForTradeInReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([retail, wholesale]);

        var result = await new TradeInsByBranchHandler(
                _currentUser.Object, _sales.Object, _products.Object, _branches.Object)
            .Handle(new TradeInsByBranchQuery(From, To, SaleType: saleType), CancellationToken.None);

        result.Value.Rows.Sum(row => row.Units).Should().Be(expectedUnits);
    }

    [Fact]
    public async Task Discounts_FiltersByCustomer()
    {
        StoreDiscountSales(Discounted(_ana, "A-1"), Discounted(_beto, "B-1"));

        var result = await DiscountsHandler().Handle(
            new DiscountsReportQuery(From, To, CustomerId: _ana.Id.Value), CancellationToken.None);

        result.Value.Rows.Select(row => row.SaleCode).Should().Equal("A-1");
        result.Value.Totals.SalesCount.Should().Be(1);
    }

    [Theory]
    [InlineData("wholesale", "CC-1")]
    [InlineData("retail", "R-1")]
    public async Task Discounts_FiltersBySaleType(string saleType, string expected)
    {
        var wholesale = Sale.CreateCc(_companyId, _mataderos.Id, _ana.Id,
            [SaleDetail.Create(_battery.Id, 1, 100m)], code: "CC-1", generalDiscountPercent: 10m);
        StoreDiscountSales(Discounted(_ana, "R-1"), wholesale);

        var result = await DiscountsHandler().Handle(
            new DiscountsReportQuery(From, To, SaleType: saleType), CancellationToken.None);

        result.Value.Rows.Select(row => row.SaleCode).Should().Equal(expected);
    }

    // Minorista sale de los pagos de la venta y mayorista de los cobros de cuenta corriente: el cliente filtra los dos.
    [Fact]
    public async Task PaymentMethods_FiltersBothPaymentSourcesByCustomer()
    {
        var ofAna = Sale.Create(_companyId, _mataderos.Id, _ana.Id, false, SaleStatus.Paid,
            [SaleDetail.Create(_battery.Id, 1, 100m)], [SalePayment.Create(SalePaymentMethod.Cash, 100m, null)], allowOverpayment: true);
        var ofBeto = Sale.Create(_companyId, _mataderos.Id, _beto.Id, false, SaleStatus.Paid,
            [SaleDetail.Create(_battery.Id, 1, 200m)], [SalePayment.Create(SalePaymentMethod.Cash, 200m, null)], allowOverpayment: true);
        var walkIn = Sale.Create(_companyId, _mataderos.Id, null, false, SaleStatus.Paid,
            [SaleDetail.Create(_battery.Id, 1, 400m)], [SalePayment.Create(SalePaymentMethod.Cash, 400m, null)], allowOverpayment: true);
        _sales.Setup(r => r.ListWithPaymentsForReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ofAna, ofBeto, walkIn]);
        _customerPayments.Setup(r => r.ListForPaymentMethodsReportAsync(
                _companyId.Value, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CcPayment(_ana, 30m), CcPayment(_beto, 50m)]);

        var result = await new PaymentMethodsReportHandler(
                _currentUser.Object, _sales.Object, _customerPayments.Object, _banks.Object)
            .Handle(new PaymentMethodsReportQuery(From, To, CustomerId: _ana.Id.Value), CancellationToken.None);

        result.Value.Rows.Sum(row => row.Total).Should().Be(130m);
    }

    [Fact]
    public async Task PaymentMethods_ABlankSaleType_MeansAll()
    {
        _sales.Setup(r => r.ListWithPaymentsForReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _customerPayments.Setup(r => r.ListForPaymentMethodsReportAsync(
                _companyId.Value, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CcPayment(_ana, 30m)]);

        var result = await new PaymentMethodsReportHandler(
                _currentUser.Object, _sales.Object, _customerPayments.Object, _banks.Object)
            .Handle(new PaymentMethodsReportQuery(From, To, SaleType: "  "), CancellationToken.None);

        result.Value.Rows.Sum(row => row.Total).Should().Be(30m);
    }

    [Fact]
    public async Task Cash_AsksOnlyForTheRequestedBranch()
    {
        await CashHandler().Handle(new CashMovementsReportQuery(From, To, _mataderos.Id.Value), CancellationToken.None);

        VerifyCashBranches(ids => ids != null && ids.SequenceEqual(new[] { _mataderos.Id.Value }));
    }

    [Fact]
    public async Task Cash_WithoutBranch_KeepsAllTheBranchesTheUserCanSee()
    {
        await CashHandler().Handle(new CashMovementsReportQuery(From, To), CancellationToken.None);

        VerifyCashBranches(ids => ids == null);
    }

    [Fact]
    public async Task Cash_ABranchTheUserCannotSee_ReturnsNothing()
    {
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(false);
        _currentUser.SetupGet(s => s.AllowedBranchIds).Returns([_mataderos.Id.Value]);

        await CashHandler().Handle(new CashMovementsReportQuery(From, To, _sanCristobal.Id.Value), CancellationToken.None);

        VerifyCashBranches(ids => ids != null && ids.Count == 0);
    }

    [Fact]
    public async Task Debtors_CountOnlyTheDebtOfTheRequestedBranch()
    {
        StoreCcSales(Wholesale(_mataderos, _ana, 100m, "M-1"), Wholesale(_sanCristobal, _ana, 200m, "S-1"));

        var result = await DebtorsHandler().Handle(
            new CustomerDebtorsQuery(BranchId: _mataderos.Id.Value), CancellationToken.None);

        result.Value.Rows.Should().ContainSingle().Which.Owed.Should().Be(100m);
    }

    [Fact]
    public async Task Debtors_FiltersByCustomer()
    {
        StoreCcSales(Wholesale(_mataderos, _ana, 100m, "A-1"), Wholesale(_mataderos, _beto, 200m, "B-1"));

        var result = await DebtorsHandler().Handle(
            new CustomerDebtorsQuery(CustomerId: _beto.Id.Value), CancellationToken.None);

        result.Value.Rows.Should().ContainSingle().Which.CustomerId.Should().Be(_beto.Id.Value);
    }

    [Fact]
    public async Task Stock_ShowsOnlyTheRequestedBranch()
    {
        var inMataderos = BranchProductStock.Create(_companyId, _mataderos.Id, _battery.Id);
        inMataderos.ApplyManualEntry(5);
        var inSanCristobal = BranchProductStock.Create(_companyId, _sanCristobal.Id, _battery.Id);
        inSanCristobal.ApplyManualEntry(3);
        _stocks.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([inMataderos, inSanCristobal]);

        var result = await new StockMatrixHandler(
                _currentUser.Object, _branches.Object, _products.Object, _stocks.Object)
            .Handle(new StockMatrixQuery(_mataderos.Id.Value), CancellationToken.None);

        result.Value.Branches.Select(branch => branch.Name).Should().Equal("Mataderos");
        result.Value.Rows.Should().ContainSingle().Which.Total.Should().Be(5);
    }

    [Fact]
    public async Task Stock_ABranchTheUserCannotSee_ReturnsNothing()
    {
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(false);
        _currentUser.SetupGet(s => s.AllowedBranchIds).Returns([_mataderos.Id.Value]);
        _stocks.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await new StockMatrixHandler(
                _currentUser.Object, _branches.Object, _products.Object, _stocks.Object)
            .Handle(new StockMatrixQuery(_sanCristobal.Id.Value), CancellationToken.None);

        result.Value.Branches.Should().BeEmpty();
        result.Value.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Validators_RejectAnUnknownSaleType()
    {
        new DailySalesControlValidator().Validate(new DailySalesControlQuery(From, To, SaleType: "otro")).IsValid.Should().BeFalse();
        new TradeInsByBranchValidator().Validate(new TradeInsByBranchQuery(From, To, SaleType: "otro")).IsValid.Should().BeFalse();
        new DiscountsReportValidator().Validate(new DiscountsReportQuery(From, To, SaleType: "otro")).IsValid.Should().BeFalse();
        new PaymentMethodsReportValidator().Validate(new PaymentMethodsReportQuery(From, To, SaleType: "otro")).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("all")]
    [InlineData("wholesale")]
    [InlineData("retail")]
    [InlineData(null)]
    public void Validators_AcceptTheKnownSaleTypes(string? saleType)
    {
        new DailySalesControlValidator().Validate(new DailySalesControlQuery(From, To, SaleType: saleType)).IsValid.Should().BeTrue();
        new TradeInsByBranchValidator().Validate(new TradeInsByBranchQuery(From, To, SaleType: saleType)).IsValid.Should().BeTrue();
        new DiscountsReportValidator().Validate(new DiscountsReportQuery(From, To, SaleType: saleType)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Endpoints_PassTheFiltersToTheirQueries()
    {
        var branch = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var user = Guid.NewGuid();

        (await SentBy<DailySalesControlResponse>(c => c.DailySalesControl(From, To, 2, branch, customer, "retail", default)))
            .Should().Be(new DailySalesControlQuery(From, To, 2, branch, customer, "retail"));
        (await SentBy<TradeInsByBranchResponse>(c => c.TradeInsByBranch(From, To, branch, customer, "retail", default)))
            .Should().Be(new TradeInsByBranchQuery(From, To, branch, customer, "retail"));
        (await SentBy<DiscountsReportResponse>(c => c.DiscountsReport(From, To, branch, user, customer, "retail", default)))
            .Should().Be(new DiscountsReportQuery(From, To, branch, user, customer, "retail"));
        (await SentBy<PaymentMethodsReportResponse>(c => c.PaymentMethods(From, To, branch, "retail", customer, default)))
            .Should().Be(new PaymentMethodsReportQuery(From, To, branch, "retail", customer));
        (await SentBy<CashMovementsReportResponse>(c => c.CashMovements(From, To, branch, default)))
            .Should().Be(new CashMovementsReportQuery(From, To, branch));
        (await SentBy<CustomerDebtorsResponse>(c => c.CustomerDebtors(branch, customer, default)))
            .Should().Be(new CustomerDebtorsQuery(branch, customer));
        (await SentBy<StockMatrixResponse>(c => c.StockMatrix(branch, default)))
            .Should().Be(new StockMatrixQuery(branch));
    }

    private static async Task<object?> SentBy<TResponse>(Func<ReportsController, Task<IActionResult>> call)
    {
        object? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<IRequest<Result<TResponse>>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<TResponse>>, CancellationToken>((query, _) => sent = query)
            .ReturnsAsync(Result<TResponse>.Failure(Error.Validation("Test.Stop", "solo interesa la query")));

        await call(new ReportsController(sender.Object));

        return sent;
    }

    private Sale Retail(Branch branch, Customer customer, decimal price, string code) =>
        Sale.Create(_companyId, branch.Id, customer.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, price)], code: code);

    private Sale Wholesale(Branch branch, Customer customer, decimal price, string code) =>
        Sale.CreateCc(_companyId, branch.Id, customer.Id, [SaleDetail.Create(_battery.Id, 1, price)], code: code);

    private Sale Discounted(Customer customer, string code) =>
        Sale.Create(_companyId, _mataderos.Id, customer.Id, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, 100m)], code: code, generalDiscountPercent: 10m);

    private CustomerPayment CcPayment(Customer customer, decimal amount) =>
        CustomerPayment.Create(_companyId.Value, customer.Id.Value, _mataderos.Id.Value, SalePaymentMethod.Cash,
            amount, From, null, null, Guid.NewGuid());

    private void StoreDailySales(params Sale[] sales) =>
        _sales.Setup(r => r.ListByCompanyAsync(
                _companyId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);

    private void StoreDiscountSales(params Sale[] sales) =>
        _sales.Setup(r => r.ListForDiscountReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);

    // Igual que el repositorio: si llega un cliente, solo devuelve sus ventas.
    private void StoreCcSales(params Sale[] sales) =>
        _sales.Setup(r => r.ListCcSalesByCompanyAsync(_companyId, It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyId _, CustomerId? customerId, CancellationToken _) =>
                sales.Where(sale => customerId is null || sale.CustomerId == customerId).ToList());

    private void VerifyCashBranches(System.Linq.Expressions.Expression<Func<IReadOnlyCollection<Guid>?, bool>> match) =>
        _cashSessions.Verify(r => r.ListMovementsByCompanyAsync(
            _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<CashMovementType>>(),
            It.Is(match), It.IsAny<CancellationToken>()));

    private DailySalesControlHandler DailyHandler() =>
        new(_currentUser.Object, _sales.Object, _products.Object, _customers.Object, _branches.Object);

    private DiscountsReportHandler DiscountsHandler() =>
        new(_currentUser.Object, _sales.Object, _branches.Object, _customers.Object, _users.Object);

    private CashMovementsReportHandler CashHandler() =>
        new(_currentUser.Object, _cashSessions.Object, _users.Object);

    private CustomerDebtorsHandler DebtorsHandler() =>
        new(_currentUser.Object, _sales.Object, _customers.Object);
}
