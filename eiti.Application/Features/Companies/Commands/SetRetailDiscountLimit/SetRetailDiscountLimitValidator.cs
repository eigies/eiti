using FluentValidation;

namespace eiti.Application.Features.Companies.Commands.SetRetailDiscountLimit;

public sealed class SetRetailDiscountLimitValidator : AbstractValidator<SetRetailDiscountLimitCommand>
{
    public SetRetailDiscountLimitValidator()
    {
        RuleFor(x => x.MaxPercent)
            .InclusiveBetween(0m, 100m).WithMessage("El descuento máximo tiene que estar entre 0 y 100%.");
    }
}
