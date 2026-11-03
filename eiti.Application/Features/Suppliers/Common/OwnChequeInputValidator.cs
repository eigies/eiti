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
        RuleFor(x => x.FechaEmision)
            .NotEmpty().WithMessage("Cargá la fecha de emisión del cheque propio.");
        RuleFor(x => x.FechaVencimiento)
            .NotEmpty().WithMessage("Cargá la fecha de pago del cheque propio.");
        // El cheque guarda solo el día, así que la comparación también.
        RuleFor(x => x.FechaVencimiento)
            .Must((input, vencimiento) => vencimiento.Date >= input.FechaEmision.Date)
            .WithMessage("La fecha de pago del cheque no puede ser anterior a la de emisión.");
    }
}
