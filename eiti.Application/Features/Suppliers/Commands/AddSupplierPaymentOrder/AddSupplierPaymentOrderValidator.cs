using eiti.Application.Features.Suppliers.Common;
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
                .NotEmpty().WithMessage("Elegí un cheque en cartera o cargá uno propio.")
                .When(l => l.Method == (int)PurchasePaymentMethod.Check && l.OwnCheque is null);
            line.RuleFor(l => l.ChequeId)
                .Empty().WithMessage("Un cheque es de cartera o propio, no los dos.")
                .When(l => l.OwnCheque is not null);
            line.RuleFor(l => l.OwnCheque)
                .Null().WithMessage("Un cheque propio solo va con el medio Cheque.")
                .When(l => l.Method != (int)PurchasePaymentMethod.Check);
            line.RuleFor(l => l.OwnCheque!).SetValidator(new OwnChequeInputValidator()).When(l => l.OwnCheque is not null);
            line.RuleFor(l => l.Reference).MaximumLength(120).When(l => l.Reference != null);
        });
    }
}
