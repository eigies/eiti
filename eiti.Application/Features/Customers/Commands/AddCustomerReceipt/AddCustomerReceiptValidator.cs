using eiti.Domain.Sales;
using FluentValidation;

namespace eiti.Application.Features.Customers.Commands.AddCustomerReceipt;

public sealed class AddCustomerReceiptValidator : AbstractValidator<AddCustomerReceiptCommand>
{
    public const int MaxLines = 50;

    public AddCustomerReceiptValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes != null);

        RuleFor(x => x.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El cobro tiene que tener al menos un medio.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Un cobro admite hasta {MaxLines} medios.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            // CustomerCredit es la imputación interna del saldo a favor: no es un medio con el que se cobra.
            line.RuleFor(l => l.Method)
                .Must(m => Enum.IsDefined(typeof(SalePaymentMethod), m) && m != (int)SalePaymentMethod.CustomerCredit)
                .WithMessage("El método de cobro es inválido.");
            line.RuleFor(l => l.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
            line.RuleFor(l => l.Cheque)
                .NotNull().WithMessage("Debe completar los datos del cheque.")
                .When(l => l.Method == (int)SalePaymentMethod.Check);
            line.RuleFor(l => l.Reference).MaximumLength(120).When(l => l.Reference != null);
        });
    }
}
