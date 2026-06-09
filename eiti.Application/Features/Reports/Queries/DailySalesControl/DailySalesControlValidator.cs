using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.DailySalesControl;

public sealed class DailySalesControlValidator : AbstractValidator<DailySalesControlQuery>
{
    public DailySalesControlValidator()
    {
        RuleFor(query => query.DateFrom)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha desde es obligatoria.");

        RuleFor(query => query.DateTo)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha hasta es obligatoria.");

        RuleFor(query => query.DateFrom)
            .LessThanOrEqualTo(query => query.DateTo)
            .When(query => query.DateFrom != default && query.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");

        RuleFor(query => query.Status)
            .InclusiveBetween(0, 3)
            .WithMessage("El estado de venta es invalido.");
    }
}
