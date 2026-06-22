namespace eiti.Application.Features.Stock.Commands.ImportBranchPricing;

public sealed record ImportBranchPricingResponse(
    int TotalRows,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<ImportBranchPricingRowResponse> Rows);

public sealed record ImportBranchPricingRowResponse(
    int RowNumber,
    string Code,
    string BranchName,
    string Action,
    string Message);
