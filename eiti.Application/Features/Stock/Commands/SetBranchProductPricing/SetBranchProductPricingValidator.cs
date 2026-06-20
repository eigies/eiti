using FluentValidation;

namespace eiti.Application.Features.Stock.Commands.SetBranchProductPricing;

public sealed class SetBranchProductPricingValidator : AbstractValidator<SetBranchProductPricingCommand>
{
    public SetBranchProductPricingValidator()
    {
        RuleFor(x => x.CostOverride)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CostOverride.HasValue);

        RuleFor(x => x.SalePriceOverride)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SalePriceOverride.HasValue);
    }
}
