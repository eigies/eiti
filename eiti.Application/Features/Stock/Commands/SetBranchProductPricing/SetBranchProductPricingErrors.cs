using eiti.Application.Common;

namespace eiti.Application.Features.Stock.Commands.SetBranchProductPricing;

public static class SetBranchProductPricingErrors
{
    public static readonly Error BranchNotFound =
        Error.NotFound("Stock.SetPricing.BranchNotFound", "The selected branch was not found.");

    public static readonly Error ProductNotFound =
        Error.NotFound("Stock.SetPricing.ProductNotFound", "The selected product was not found.");

    public static readonly Error InvalidPricing =
        Error.Validation("Stock.SetPricing.Invalid", "Cost/price overrides cannot be negative.");
}
