using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.DiscountsReport;

public sealed class DiscountsReportValidator : AbstractValidator<DiscountsReportQuery>
{
    public DiscountsReportValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEmpty().WithMessage("La fecha desde es obligatoria.");

        RuleFor(x => x.DateTo)
            .NotEmpty().WithMessage("La fecha hasta es obligatoria.")
            .GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha hasta no puede ser anterior a la fecha desde.");
    }
}
