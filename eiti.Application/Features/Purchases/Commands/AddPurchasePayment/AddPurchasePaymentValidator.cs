using eiti.Domain.Purchases;
using FluentValidation;

namespace eiti.Application.Features.Purchases.Commands.AddPurchasePayment;

public sealed class AddPurchasePaymentValidator : AbstractValidator<AddPurchasePaymentCommand>
{
    public AddPurchasePaymentValidator()
    {
        RuleFor(x => x.PurchaseId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        RuleFor(x => x.Method)
            .Must(m => Enum.IsDefined(typeof(PurchasePaymentMethod), m))
            .WithMessage("Invalid payment method.");
        RuleFor(x => x.Reference)
            .MaximumLength(120).WithMessage("Reference cannot exceed 120 characters.")
            .When(x => x.Reference != null);
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes != null);
        RuleFor(x => x.IvaPct)
            .Must(v => v == null || v == 10.5m || v == 21.0m)
            .WithMessage("IvaPct must be null (Exento), 10.5, or 21.");
        RuleFor(x => x.IngresosBrutosPct)
            .GreaterThanOrEqualTo(0).When(x => x.IngresosBrutosPct.HasValue)
            .WithMessage("IngresosBrutosPct must be >= 0.");
    }
}
