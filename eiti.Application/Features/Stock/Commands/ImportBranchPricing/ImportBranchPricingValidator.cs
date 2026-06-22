using FluentValidation;

namespace eiti.Application.Features.Stock.Commands.ImportBranchPricing;

public sealed class ImportBranchPricingValidator : AbstractValidator<ImportBranchPricingCommand>
{
    public ImportBranchPricingValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty()
            .WithMessage(ImportBranchPricingErrors.RowsRequired.Description);
    }
}
