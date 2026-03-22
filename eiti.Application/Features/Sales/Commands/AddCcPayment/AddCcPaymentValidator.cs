using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.AddCcPayment;

public sealed class AddCcPaymentValidator : AbstractValidator<AddCcPaymentCommand>
{
    public AddCcPaymentValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("Sale id is required.");

        RuleFor(x => x.IdPaymentMethod)
            .InclusiveBetween(1, 5).WithMessage("A valid payment method is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Payment amount must be greater than zero.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Payment date is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(250).WithMessage("Notes must be at most 250 characters.");
    }
}
