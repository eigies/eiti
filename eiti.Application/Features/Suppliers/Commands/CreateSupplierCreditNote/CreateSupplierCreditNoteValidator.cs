using FluentValidation;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;

public sealed class CreateSupplierCreditNoteValidator : AbstractValidator<CreateSupplierCreditNoteCommand>
{
    public CreateSupplierCreditNoteValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El importe de la nota de crédito debe ser mayor a cero.");

        // El motivo es la única trazabilidad de un ajuste sin documento de origen.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de la nota de crédito es obligatorio.")
            .MaximumLength(250).WithMessage("El motivo no puede superar los 250 caracteres.");

        RuleFor(x => x.Date).NotEmpty();
    }
}
