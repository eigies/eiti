using FluentValidation;

namespace eiti.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Product code is required.")
            .MaximumLength(50).WithMessage("Product code cannot exceed 50 characters.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Product SKU is required.")
            .MaximumLength(80).WithMessage("Product SKU cannot exceed 80 characters.");

        RuleFor(x => x.Brand)
            .NotEmpty().WithMessage("Product brand is required.")
            .MaximumLength(100).WithMessage("Product brand cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(150).WithMessage("Product name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Product description cannot exceed 1000 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Price.HasValue)
            .WithMessage("Product price cannot be negative.");

        RuleFor(x => x.PublicPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PublicPrice.HasValue)
            .WithMessage("Product public price cannot be negative.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Product cost price cannot be negative.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UnitPrice.HasValue)
            .WithMessage("Product unit price cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.Price.HasValue || x.PublicPrice.HasValue)
            .WithMessage("Either product price or public price is required.");

        RuleFor(x => x)
            .Must(x => !x.Price.HasValue || !x.PublicPrice.HasValue || x.Price.Value == x.PublicPrice.Value)
            .WithMessage("When both price and public price are provided, they must be equal.");
    }
}
