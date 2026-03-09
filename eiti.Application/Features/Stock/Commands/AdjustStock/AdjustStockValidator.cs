using FluentValidation;

namespace eiti.Application.Features.Stock.Commands.AdjustStock;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Quantity).NotEqual(0);
        RuleFor(command => command.Description).MaximumLength(255);
    }
}
