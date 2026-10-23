using eiti.Domain.Purchases;
using FluentValidation;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

public sealed class AddSupplierPaymentOrderValidator : AbstractValidator<AddSupplierPaymentOrderCommand>
{
    public const int MaxLines = 50;

    public AddSupplierPaymentOrderValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes != null);

        RuleFor(x => x.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El pago tiene que tener al menos un medio.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Un pago admite hasta {MaxLines} medios.");

        RuleFor(x => x.Lines)
            .Must(lines => lines
                .Where(l => l.ChequeId.HasValue)
                .GroupBy(l => l.ChequeId)
                .All(g => g.Count() == 1))
            .WithMessage("El mismo cheque está cargado dos veces en el pago.")
            .When(x => x.Lines != null);

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            // SupplierCredit es la imputación interna del saldo a favor: no es un medio con el que se paga.
            line.RuleFor(l => l.Method)
                .Must(m => Enum.IsDefined(typeof(PurchasePaymentMethod), m) && m != (int)PurchasePaymentMethod.SupplierCredit)
                .WithMessage("El método de pago es inválido.");
            line.RuleFor(l => l.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
            line.RuleFor(l => l.ChequeId)
                .NotEmpty().WithMessage("Debe seleccionar un cheque en cartera.")
                .When(l => l.Method == (int)PurchasePaymentMethod.Check);
            line.RuleFor(l => l.Reference).MaximumLength(120).When(l => l.Reference != null);
        });
    }
}
