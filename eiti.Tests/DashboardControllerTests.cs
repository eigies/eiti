using eiti.Api.Controllers;
using eiti.Application.Common;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Application.Features.Dashboard.Queries.ListDashboardSales;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace eiti.Tests;

public sealed class DashboardControllerTests
{
    [Fact]
    public async Task Summary_EnviaLosFiltrosYDevuelveOk()
    {
        var dateFrom = new DateTime(2026, 8, 1);
        var dateTo = new DateTime(2026, 8, 31);
        var branchId = Guid.NewGuid();
        var response = EmptyResponse();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetDashboardSummaryQuery>(q =>
                    q.DateFrom == dateFrom && q.DateTo == dateTo && q.BranchId == branchId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GetDashboardSummaryResponse>.Success(response));
        var controller = new DashboardController(sender.Object);

        var action = await controller.Summary(dateFrom, dateTo, branchId, CancellationToken.None);

        action.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(response);
        sender.VerifyAll();
    }

    [Fact]
    public async Task Sales_EnviaLosFiltrosYDevuelveOk()
    {
        var dateFrom = new DateTime(2026, 8, 15);
        var dateTo = new DateTime(2026, 8, 15);
        var branchId = Guid.NewGuid();
        IReadOnlyList<DashboardSaleResponse> response = [];
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.Is<ListDashboardSalesQuery>(q =>
                    q.DateFrom == dateFrom && q.DateTo == dateTo && q.BranchId == branchId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<DashboardSaleResponse>>.Success(response));
        var controller = new DashboardController(sender.Object);

        var action = await controller.Sales(dateFrom, dateTo, branchId, CancellationToken.None);

        action.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(response);
        sender.VerifyAll();
    }

    private static GetDashboardSummaryResponse EmptyResponse() =>
        new(
            new DashboardPeriodTotals(
                new DashboardSegment(0, 0m),
                new DashboardSegment(0, 0m),
                new DashboardSegment(0, 0m)),
            new DashboardPeriodTotals(
                new DashboardSegment(0, 0m),
                new DashboardSegment(0, 0m),
                new DashboardSegment(0, 0m)),
            [],
            [],
            new DashboardCollections(0m, 0, 0m, 0, 0m),
            new DashboardTodayStatus(0, 0, 0, 0),
            []);
}
