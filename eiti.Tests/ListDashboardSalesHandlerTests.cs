using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Dashboard.Queries.ListDashboardSales;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class ListDashboardSalesHandlerTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly BranchId BranchA = BranchId.New();
    private static readonly BranchId BranchB = BranchId.New();
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 8, 15, 15, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task DevuelveCanceladasSinExigirSalesAccess()
    {
        var cancelled = Sale.Create(
            Company,
            BranchA,
            null,
            false,
            SaleStatus.Paid,
            [SaleDetail.Create(ProductId.New(), 1, 120m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 120m, null)],
            allowOverpayment: true);
        cancelled.Cancel();
        var (handler, repository) = BuildHandler([cancelled]);

        var result = await handler.Handle(
            new ListDashboardSalesQuery(
                new DateTime(2026, 8, 15),
                new DateTime(2026, 8, 15),
                BranchA.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(item => item.SaleStatus == (int)SaleStatus.Cancel);
        repository.Verify(r => r.ListForDashboardAsync(
            Company,
            BusinessCalendar.StartOfDayUtc(new DateTime(2026, 8, 15)),
            BusinessCalendar.EndOfDayUtc(new DateTime(2026, 8, 15)),
            BranchA.Value,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SinPermisoFinanciero_OcultaImportes()
    {
        var sale = Sale.Create(
            Company,
            BranchA,
            null,
            false,
            SaleStatus.Paid,
            [SaleDetail.Create(ProductId.New(), 1, 120m)],
            [SalePayment.Create(SalePaymentMethod.Cash, 120m, null)],
            allowOverpayment: true);
        var (handler, _) = BuildHandler([sale], canViewFinancials: false);

        var result = await handler.Handle(
            new ListDashboardSalesQuery(
                new DateTime(2026, 8, 15),
                new DateTime(2026, 8, 15)),
            CancellationToken.None);

        result.Value.Should().ContainSingle().Which.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task SucursalAjena_EsRechazadaAntesDeConsultar()
    {
        var (handler, repository) = BuildHandler(
            [], canViewAll: false, allowedBranches: [BranchA.Value]);

        var result = await handler.Handle(
            new ListDashboardSalesQuery(
                new DateTime(2026, 8, 15),
                new DateTime(2026, 8, 15),
                BranchB.Value),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.Summary.BranchNotAllowed");
        repository.Verify(r => r.ListForDashboardAsync(
            It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2026, 8, 8)]
    [InlineData(2026, 8, 16)]
    public async Task FechaFueraDeLosUltimosSieteDias_EsRechazada(
        int year, int month, int day)
    {
        var (handler, repository) = BuildHandler([]);
        var date = new DateTime(year, month, day);

        var result = await handler.Handle(
            new ListDashboardSalesQuery(date, date),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.Sales.DateOutsideWindow");
        repository.Verify(r => r.ListForDashboardAsync(
            It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RangoDeMasDeUnDia_EsRechazado()
    {
        var (handler, repository) = BuildHandler([]);

        var result = await handler.Handle(
            new ListDashboardSalesQuery(
                new DateTime(2026, 8, 14),
                new DateTime(2026, 8, 15)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.Sales.SingleDayRequired");
        repository.Verify(r => r.ListForDashboardAsync(
            It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ListDashboardSalesHandler Handler, Mock<ISaleRepository> Repository) BuildHandler(
        IReadOnlyList<Sale> sales,
        bool canViewFinancials = true,
        bool canViewAll = true,
        IReadOnlyCollection<Guid>? allowedBranches = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.CompanyId).Returns(Company);
        user.SetupGet(x => x.CanViewAllBranches).Returns(canViewAll);
        user.SetupGet(x => x.AllowedBranchIds).Returns(allowedBranches ?? []);
        user.Setup(x => x.HasPermission(PermissionCodes.DashboardViewFinancials))
            .Returns(canViewFinancials);

        var repository = new Mock<ISaleRepository>();
        repository.Setup(r => r.ListForDashboardAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.ListByIdsAsync(
                It.IsAny<CompanyId>(), It.IsAny<IEnumerable<CustomerId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return (new ListDashboardSalesHandler(
            user.Object, repository.Object, customers.Object, Clock), repository);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
