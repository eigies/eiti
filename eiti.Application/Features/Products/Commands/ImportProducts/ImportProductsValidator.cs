using FluentValidation;

namespace eiti.Application.Features.Products.Commands.ImportProducts;

public sealed class ImportProductsValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty()
            .WithMessage(ImportProductsErrors.RowsRequired.Description);
    }
}
