using eiti.Domain.Sales;
using FluentValidation;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

public sealed class AddCustomerPaymentValidator : AbstractValidator<AddCustomerPaymentCommand>
{
    public AddCustomerPaymentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Method)
            .Must(m => Enum.IsDefined(typeof(SalePaymentMethod), m) && m != (int)SalePaymentMethod.CustomerCredit)
            .WithMessage("El método de cobro es inválido.");
        RuleFor(x => x.Cheque)
            .NotNull().WithMessage("Debe completar los datos del cheque.")
            .When(x => x.Method == (int)SalePaymentMethod.Check);
        RuleFor(x => x.Reference)
            .MaximumLength(120).When(x => x.Reference != null);
        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes != null);
    }
}
