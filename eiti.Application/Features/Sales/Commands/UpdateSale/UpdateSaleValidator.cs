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
    }
}
