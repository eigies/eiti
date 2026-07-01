using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed class CreateCcSaleValidator : AbstractValidator<CreateCcSaleCommand>
{
    public CreateCcSaleValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch id is required.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer id is required for Cuenta Corriente sales.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("At least one sale detail is required.");

        RuleForEach(x => x.Details)
            .ChildRules(detail =>
            {
                detail.RuleFor(x => x.ProductId)
                    .NotEmpty().WithMessage("Product id is required.");

                detail.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            });

        RuleForEach(x => x.TradeIns)
            .ChildRules(tradeIn =>
            {
                tradeIn.RuleFor(x => x.ProductId)
                    .NotEmpty().WithMessage("Trade-in product id is required.");

                tradeIn.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Trade-in quantity must be greater than zero.");

                tradeIn.RuleFor(x => x.Amount)
                    .GreaterThanOrEqualTo(0m).WithMessage("Trade-in amount cannot be negative.");
            })
            .When(x => x.TradeIns is not null);
    }
}
