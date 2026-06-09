using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.SalesReport;

public sealed record SalesReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    string GroupBy,
    Guid? CustomerId = null,
    Guid? InstallerId = null,
    Guid? VehicleId = null,
    int? Channel = null,
    string? DeliveryMode = null,
    Guid? CategoryId = null
) : IRequest<Result<SalesReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [PermissionForGroupBy(GroupBy)];

    private static string PermissionForGroupBy(string groupBy) => (groupBy ?? string.Empty).ToLowerInvariant() switch
    {
        "product" => PermissionCodes.ReportsSalesModel,
        "product_ranking" => PermissionCodes.ReportsSalesRanking,
        "brand" => PermissionCodes.ReportsSalesBrand,
        "channel" => PermissionCodes.ReportsSalesChannel,
        "channel_brand" => PermissionCodes.ReportsSalesChannelBrand,
        "installer" => PermissionCodes.ReportsSalesTransport,
        "vehicle" => PermissionCodes.ReportsSalesTransport,
        "total" => PermissionCodes.ReportsSalesComparison,
        _ => PermissionCodes.ReportsSalesModel
    };
}
