using FluentValidation;

namespace eiti.Application.Features.Suppliers.Common;

// Lo comparten el pago suelto y la orden de pago.
public sealed class OwnChequeInputValidator : AbstractValidator<OwnChequeInput>
{
    public OwnChequeInputValidator()
    {
        RuleFor(x => x.BankId).GreaterThan(0).WithMessage("Elegí el banco del cheque propio.");
        RuleFor(x => x.Numero)
            .Must(numero => !string.IsNullOrWhiteSpace(numero)).WithMessage("Cargá el número del cheque propio.")
            .MaximumLength(50);
        RuleFor(x => x.FechaVencimiento)
            .GreaterThanOrEqualTo(x => x.FechaEmision)
            .WithMessage("La fecha de pago del cheque no puede ser anterior a la de emisión.");
    }
}
