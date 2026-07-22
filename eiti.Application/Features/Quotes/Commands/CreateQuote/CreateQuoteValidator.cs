using FluentValidation;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch id is required.");

        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue != !string.IsNullOrWhiteSpace(x.ProspectName))
            .WithMessage("Provide either an existing CustomerId or a ProspectName, not both or neither.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("At least one quote detail is required.");

        RuleForEach(x => x.Details)
            .ChildRules(detail =>
            {
                detail.RuleFor(x => x.ProductId)
                    .NotEmpty().WithMessage("Product id is required.");

                detail.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

                detail.RuleFor(x => x.UnitPrice)
                    .GreaterThanOrEqualTo(0m).WithMessage("Unit price cannot be negative.");

                detail.RuleFor(x => x.DiscountPercent)
                    .InclusiveBetween(0m, 100m).WithMessage("Discount percent must be between 0 and 100.");
            });

        RuleFor(x => x.GeneralDiscountPercent)
            .InclusiveBetween(0m, 100m).WithMessage("General discount percent must be between 0 and 100.");

        RuleFor(x => x.VatRate)
            .Must(rate => rate == 0m || rate == 10.5m || rate == 21m)
            .WithMessage("VatRate must be one of 0 (exento), 10.5 or 21.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("ExpiresAt must be in the future.");
    }
}
