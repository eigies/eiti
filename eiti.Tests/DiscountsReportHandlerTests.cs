using eiti.Api.Controllers;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Reports.Queries.DiscountsReport;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

// Reporte "Descuentos": una fila por venta con descuento general, para que el dueño audite qué se descontó,
// quién y por qué.
public sealed class DiscountsReportHandlerTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _centro;
    private readonly Branch _norte;
    private readonly Customer _customer;
    private readonly Guid _seller = Guid.NewGuid();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IUserRepository> _users = new();
    private List<Sale> _stored = [];

    public DiscountsReportHandlerTests()
    {
        _centro = Branch.Create(_companyId, "Centro", "C", "San Martin 123");
        _norte = Branch.Create(_companyId, "Norte", "N", "Cabildo 2000");
        _customer = Customer.Create(_companyId, "Juan", "Perez", null);
        _currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(s => s.CompanyId).Returns(_companyId);
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(true);
        _branches.Setup(r => r.ListByCompanyAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync([_centro, _norte]);
        _customers.Setup(r => r.ListByIdsAsync(_companyId, It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_customer]);
        _users.Setup(r => r.GetUsernamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_seller] = "emiliano" });
        _sales.Setup(r => r.ListForDiscountReportAsync(
                _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _stored);
    }

    [Fact]
    public async Task Handle_ListsTheSaleWithWhoAppliedTheDiscountAndWhy()
    {
        var sale = RetailSale(_centro, price: 100000m, discount: 10m, by: _seller, reason: "Cliente de años", code: "C-001");
        _stored = [sale];

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new DiscountsReportRow(
            sale.Id.Value, "C-001", sale.CreatedAt, "Centro", "Consumidor final", false,
            _seller, "emiliano", 10m, 100000m, 10000m, 90000m, "Cliente de años"));
        result.Value.Totals.Should().Be(new DiscountsReportTotals(1, 10000m));
    }

    [Fact]
    public async Task Handle_ShowsTheCustomerAndTheCuentaCorrienteType()
    {
        var sale = Sale.CreateCc(_companyId, _norte.Id, _customer.Id, [SaleDetail.Create(ProductId.New(), 2, 50000m)],
            code: "N-010", generalDiscountPercent: 20m);
        sale.TrackGeneralDiscount(0m, _seller, null);
        _stored = [sale];

        var row = (await Handler().Handle(Query(), CancellationToken.None)).Value.Rows.Single();

        row.CustomerName.Should().Be("Juan Perez");
        row.IsCuentaCorriente.Should().BeTrue();
        row.BranchName.Should().Be("Norte");
        row.DiscountAmount.Should().Be(20000m);
        row.Total.Should().Be(80000m);
    }

    [Fact]
    public async Task Handle_TheDiscountAmountIgnoresAManualOverride()
    {
        var sale = Sale.CreateCc(_companyId, _centro.Id, _customer.Id, [SaleDetail.Create(ProductId.New(), 1, 100000m)],
            generalDiscountPercent: 10m, manualOverridePrice: 85000m, overriddenByUserId: _seller);
        _stored = [sale];

        var row = (await Handler().Handle(Query(), CancellationToken.None)).Value.Rows.Single();

        row.SubtotalBeforeDiscount.Should().Be(100000m);
        row.DiscountAmount.Should().Be(10000m);
        row.Total.Should().Be(85000m);
    }

    [Fact]
    public async Task Handle_UserThatNoLongerExists_ShowsAsDeleted_AndSalesWithoutTraceShowNoUser()
    {
        var deletedUser = RetailSale(_centro, 1000m, 5m, by: Guid.NewGuid(), reason: null, code: "C-1");
        var beforeTheTrace = RetailSale(_centro, 1000m, 5m, by: null, reason: null, code: "C-2");
        _stored = [deletedUser, beforeTheTrace];

        var rows = (await Handler().Handle(Query(), CancellationToken.None)).Value.Rows;

        rows.Single(r => r.SaleCode == "C-1").AppliedByUserName.Should().Be("Usuario eliminado");
        var untraced = rows.Single(r => r.SaleCode == "C-2");
        untraced.AppliedByUserId.Should().BeNull();
        untraced.AppliedByUserName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LeavesOutCancelledSalesAndSalesWithoutDiscount()
    {
        var cancelled = RetailSale(_centro, 1000m, 5m, by: _seller, reason: null, code: "C-1");
        cancelled.Cancel();
        var noDiscount = RetailSale(_centro, 1000m, 0m, by: null, reason: null, code: "C-2");
        var kept = RetailSale(_centro, 1000m, 5m, by: _seller, reason: null, code: "C-3");
        _stored = [cancelled, noDiscount, kept];

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.Value.Rows.Select(r => r.SaleCode).Should().Equal("C-3");
        result.Value.Totals.SalesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OrdersTheRowsByDateNewestFirst_AndAddsUpTheTotals()
    {
        var older = RetailSale(_centro, 1000m, 10m, by: _seller, reason: null, code: "C-1");
        var newer = RetailSale(_centro, 3000m, 5m, by: _seller, reason: null, code: "C-2");
        SetCreatedAt(older, new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newer, new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc));
        _stored = [older, newer];

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.Value.Rows.Select(r => r.SaleCode).Should().Equal("C-2", "C-1");
        result.Value.Totals.Should().Be(new DiscountsReportTotals(2, 250m));
    }

    [Fact]
    public async Task Handle_TranslatesTheBusinessDaysToUtcAndPassesEveryFilter()
    {
        var branchId = _centro.Id.Value;
        var query = new DiscountsReportQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), branchId, _seller);
        var (from, to) = BusinessCalendar.ToUtcRange(query.DateFrom, query.DateTo);

        await Handler().Handle(query, CancellationToken.None);

        _sales.Verify(r => r.ListForDiscountReportAsync(_companyId, from, to, branchId, _seller, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_RestrictsToTheUsersBranches()
    {
        var allowed = new[] { _centro.Id.Value };
        _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(false);
        _currentUser.SetupGet(s => s.AllowedBranchIds).Returns(allowed);

        await Handler().Handle(Query(), CancellationToken.None);

        _sales.Verify(r => r.ListForDiscountReportAsync(
            _companyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, null,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(allowed)), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void Query_RequiresTheDiscountsReportPermission()
    {
        Query().RequiredPermissions.Should().Equal(PermissionCodes.ReportsSalesDiscounts);
    }

    [Fact]
    public void Validator_RejectsARangeThatEndsBeforeItStarts()
    {
        var result = new DiscountsReportValidator().Validate(
            new DiscountsReportQuery(new DateTime(2026, 10, 2), new DateTime(2026, 10, 1)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(DiscountsReportQuery.DateTo));
    }

    [Fact]
    public async Task Endpoint_PassesTheQueryStringFiltersToTheQuery()
    {
        DiscountsReportQuery? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<DiscountsReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<DiscountsReportResponse>>, CancellationToken>((q, _) => sent = (DiscountsReportQuery)q)
            .ReturnsAsync(Result<DiscountsReportResponse>.Failure(Error.Validation("Test.Stop", "solo interesa la query")));
        var branchId = Guid.NewGuid();

        await new ReportsController(sender.Object).DiscountsReport(
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), branchId, _seller, CancellationToken.None);

        sent.Should().Be(new DiscountsReportQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), branchId, _seller));
    }

    private static DiscountsReportQuery Query() => new(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

    private Sale RetailSale(Branch branch, decimal price, decimal discount, Guid? by, string? reason, string code)
    {
        var sale = Sale.Create(_companyId, branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(ProductId.New(), 1, price)], code: code, generalDiscountPercent: discount);
        sale.TrackGeneralDiscount(0m, by, reason);
        return sale;
    }

    private static void SetCreatedAt(Sale sale, DateTime createdAt) =>
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt))!.SetValue(sale, createdAt);

    private DiscountsReportHandler Handler() =>
        new(_currentUser.Object, _sales.Object, _branches.Object, _customers.Object, _users.Object);
}
