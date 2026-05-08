using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Commands.ImportProducts;

public sealed record ImportProductsCommand(
    IReadOnlyList<ImportProductRowRequest> Rows
) : IRequest<Result<ImportProductsResponse>>;

public sealed record ImportProductRowRequest(
    string Code,
    string Sku,
    string Brand,
    string Name,
    string? Description,
    decimal? PublicPrice,
    decimal CostPrice,
    decimal? UnitPrice,
    bool AllowsManualValueInSale,
    decimal? NoDeliverySurcharge,
    string? BranchName,
    int? InitialStock);
