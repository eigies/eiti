using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.WholesaleByCustomer;

public sealed class WholesaleByCustomerValidator : AbstractValidator<WholesaleByCustomerQuery>
{
    private static readonly string[] AllowedSaleTypes = ["wholesale", "retail", "all"];

    public WholesaleByCustomerValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEmpty().WithMessage("La fecha desde es obligatoria.");

        RuleFor(x => x.DateTo)
            .NotEmpty().WithMessage("La fecha hasta es obligatoria.")
            .GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha hasta no puede ser anterior a la fecha desde.");

        RuleFor(x => x.SaleType)
            .Must(type => AllowedSaleTypes.Contains((type ?? string.Empty).ToLowerInvariant()))
            .WithMessage("El tipo de venta debe ser 'wholesale', 'retail' o 'all'.");
    }
}
