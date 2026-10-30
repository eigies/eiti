using eiti.Domain.Sales;
using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.UpdateSaleNotes;

public sealed class UpdateSaleNotesValidator : AbstractValidator<UpdateSaleNotesCommand>
{
    public UpdateSaleNotesValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("Sale id is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(Sale.NotesMaxLength).WithMessage($"Las observaciones no pueden superar los {Sale.NotesMaxLength} caracteres.");
    }
}
