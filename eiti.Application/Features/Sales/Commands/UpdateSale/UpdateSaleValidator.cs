using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public sealed class UpdateSaleValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Sale id is required.");

        RuleFor(x => x.IdSaleStatus)
            .InclusiveBetween(1, 3).WithMessage("A valid sale status is required.");

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

        RuleFor(x => x.Payments)
            .NotNull().WithMessage("Payments are required.");

        RuleForEach(x => x.Payments)
            .ChildRules(payment =>
            {
                payment.RuleFor(x => x.IdPaymentMethod)
                    .InclusiveBetween(1, 5).WithMessage("A valid payment method is required.");

                payment.RuleFor(x => x.Amount)
                    .GreaterThan(0m).WithMessage("Payment amount must be greater than zero.");

                payment.RuleFor(x => x.Reference)
                    .MaximumLength(120).WithMessage("Payment reference must be at most 120 characters.");
            });

        RuleFor(x => x.TradeIns)
            .NotNull().WithMessage("Trade-ins are required.");

        RuleForEach(x => x.TradeIns)
            .ChildRules(tradeIn =>
            {
                tradeIn.RuleFor(x => x.ProductId)
                    .NotEmpty().WithMessage("Trade-in product id is required.");

                tradeIn.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Trade-in quantity must be greater than zero.");

                tradeIn.RuleFor(x => x.Amount)
                    .GreaterThanOrEqualTo(0m).WithMessage("Trade-in amount cannot be negative.");
            });
    }
}
