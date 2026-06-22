using eiti.Application.Common;

namespace eiti.Application.Features.Stock.Commands.ImportBranchPricing;

public static class ImportBranchPricingErrors
{
    public static readonly Error RowsRequired =
        Error.Validation("Stock.ImportPricing.RowsRequired", "At least one pricing row is required.");
}
