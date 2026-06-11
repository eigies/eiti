using eiti.Domain.Purchases;
using FluentValidation;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public sealed class AddSupplierPaymentValidator : AbstractValidator<AddSupplierPaymentCommand>
{
    public AddSupplierPaymentValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        RuleFor(x => x.Method)
            .Must(m => Enum.IsDefined(typeof(PurchasePaymentMethod), m))
            .WithMessage("Invalid payment method.");
        RuleFor(x => x.ChequeId)
            .NotEmpty().WithMessage("Debe seleccionar un cheque en cartera.")
            .When(x => x.Method == (int)PurchasePaymentMethod.Check);
        RuleFor(x => x.Reference)
            .MaximumLength(120).When(x => x.Reference != null);
        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes != null);
    }
}
