using eiti.Application.Features.Sales.Common;
using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

public sealed class InvoiceSaleValidator : AbstractValidator<InvoiceSaleCommand>
{
    public InvoiceSaleValidator()
    {
        RuleFor(x => x.InvoiceLetter)
            .Must(letter => Enum.IsDefined(typeof(InvoiceLetter), letter!.Value))
            .When(x => x.InvoiceLetter.HasValue)
            .WithMessage("El tipo de factura no es válido.");
    }
}
