using eiti.Domain.Purchases;
using FluentValidation;

namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public sealed class CreatePurchaseValidator : AbstractValidator<CreatePurchaseCommand>
{
    public CreatePurchaseValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("At least one purchase detail is required.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId).NotEmpty();
            detail.RuleFor(d => d.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            detail.RuleFor(d => d.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        });

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Method)
                .Must(m => Enum.IsDefined(typeof(PurchasePaymentMethod), m))
                .WithMessage("Invalid payment method.");
            payment.RuleFor(p => p.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        });

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(100).WithMessage("Invoice number cannot exceed 100 characters.")
            .When(x => x.InvoiceNumber != null);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes != null);
    }
}
