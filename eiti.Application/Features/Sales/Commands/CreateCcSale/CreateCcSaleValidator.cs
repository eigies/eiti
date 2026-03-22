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
    }
}
