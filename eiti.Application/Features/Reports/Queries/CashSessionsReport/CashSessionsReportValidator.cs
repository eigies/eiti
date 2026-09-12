using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.CashSessionsReport;

public sealed class CashSessionsReportValidator : AbstractValidator<CashSessionsReportQuery>
{
    public CashSessionsReportValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
        RuleFor(x => x)
            .Must(x => (x.DateTo.Date - x.DateFrom.Date).TotalDays <= 31)
            .When(x => x.DateFrom != default && x.DateTo != default && x.DateFrom <= x.DateTo)
            .WithMessage("El rango no puede superar los 31 días.");
    }
}
