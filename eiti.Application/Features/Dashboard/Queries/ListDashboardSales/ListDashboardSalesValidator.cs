using FluentValidation;

namespace eiti.Application.Features.Dashboard.Queries.ListDashboardSales;

public sealed class ListDashboardSalesValidator : AbstractValidator<ListDashboardSalesQuery>
{
    public ListDashboardSalesValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
        RuleFor(x => x.DateTo)
            .Must((query, dateTo) => dateTo.Date == query.DateFrom.Date)
            .When(x => x.DateFrom != default && x.DateTo != default && x.DateFrom <= x.DateTo)
            .WithMessage("El detalle del dashboard admite un solo dia por consulta.");
    }
}
