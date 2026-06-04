using FluentValidation;

namespace eiti.Application.Features.Stock.Commands.TransferStock;

public sealed class TransferStockValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockValidator()
    {
        RuleFor(command => command.SourceBranchId).NotEmpty();
        RuleFor(command => command.DestinationBranchId).NotEmpty();
        RuleFor(command => command.DestinationBranchId)
            .NotEqual(command => command.SourceBranchId)
            .WithMessage("La sucursal de origen y la de destino deben ser distintas.");

        RuleFor(command => command.Items)
            .NotEmpty()
            .WithMessage("Debés agregar al menos un producto a transferir.");

        RuleFor(command => command.Items)
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("No podés repetir el mismo producto en el traspaso.")
            .When(command => command.Items is { Count: > 0 });

        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.ProductId).NotEmpty();
            item.RuleFor(line => line.Quantity).GreaterThan(0);
        });

        RuleFor(command => command.Description).MaximumLength(255);
    }
}
