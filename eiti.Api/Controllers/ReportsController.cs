using eiti.Api.Extensions;
using eiti.Application.Features.Reports.Queries.CashMovementsReport;
using eiti.Application.Features.Reports.Queries.CustomerDebtors;
using eiti.Application.Features.Reports.Queries.DailySalesControl;
using eiti.Application.Features.Reports.Queries.ListAuditLog;
using eiti.Application.Features.Reports.Queries.PaymentMethodsReport;
using eiti.Application.Features.Reports.Queries.SalesReport;
using eiti.Application.Features.Reports.Queries.StockMovementsReport;
using eiti.Application.Features.Reports.Queries.StockMatrix;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("audit")]
    public async Task<IActionResult> ListAudit(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ListAuditLogQuery(dateFrom, dateTo, userId, page, pageSize),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("sales")]
    public async Task<IActionResult> SalesReport(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] string groupBy,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? installerId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] int? channel,
        [FromQuery] string? deliveryMode,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? saleType,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new SalesReportQuery(dateFrom, dateTo, groupBy ?? "product", customerId, installerId, vehicleId, channel, deliveryMode, categoryId, saleType, branchId),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("sales/daily-control")]
    public async Task<IActionResult> DailySalesControl(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] int status = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new DailySalesControlQuery(dateFrom, dateTo, status),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("customers/debtors")]
    public async Task<IActionResult> CustomerDebtors(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CustomerDebtorsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("stock-matrix")]
    public async Task<IActionResult> StockMatrix(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new StockMatrixQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("stock-movements")]
    public async Task<IActionResult> StockMovements(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? branchId,
        [FromQuery] int? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new StockMovementsReportQuery(dateFrom, dateTo, productId, branchId, type, page, pageSize),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("payments")]
    public async Task<IActionResult> PaymentMethods(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? branchId,
        [FromQuery] string? saleType,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new PaymentMethodsReportQuery(dateFrom, dateTo, branchId, saleType),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("cash/movements")]
    public async Task<IActionResult> CashMovements(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new CashMovementsReportQuery(dateFrom, dateTo), cancellationToken);
        return result.ToActionResult();
    }
}
