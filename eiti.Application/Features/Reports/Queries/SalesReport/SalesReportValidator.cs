using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.SalesReport;

public sealed class SalesReportValidator : AbstractValidator<SalesReportQuery>
{
    private static readonly string[] AllowedGroupBy =
    {
        "product", "product_ranking", "brand", "channel", "channel_brand", "installer", "vehicle", "total"
    };

    public SalesReportValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
        RuleFor(x => x.GroupBy)
            .Must(g => g != null && AllowedGroupBy.Contains(g.ToLowerInvariant()))
            .WithMessage("El tipo de agrupación es inválido.");
    }
}
