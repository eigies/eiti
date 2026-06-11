using eiti.Domain.Purchases;
using FluentValidation;

namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public sealed class CreatePurchaseValidator : AbstractValidator<CreatePurchaseCommand>
{
    public CreatePurchaseValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("At least one purchase detail is required.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId).NotEmpty();
            detail.RuleFor(d => d.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            detail.RuleFor(d => d.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        });

        RuleFor(x => x.IvaPct)
            .Must(v => v == null || v == 10.5m || v == 21.0m)
            .WithMessage("IvaPct must be null (Exento), 10.5, or 21.");
        RuleFor(x => x.IngresosBrutosPct)
            .GreaterThanOrEqualTo(0).When(x => x.IngresosBrutosPct.HasValue)
            .WithMessage("IngresosBrutosPct must be >= 0.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(100).WithMessage("Invoice number cannot exceed 100 characters.")
            .When(x => x.InvoiceNumber != null);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes != null);
    }
}
