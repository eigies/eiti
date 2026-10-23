using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.IncomingTransfersReport;

public sealed class IncomingTransfersReportValidator : AbstractValidator<IncomingTransfersReportQuery>
{
    public IncomingTransfersReportValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");

        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");

        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");

        // Un extracto mensual con un día de margen de cada lado entra holgado.
        RuleFor(x => x)
            .Must(x => (x.DateTo.Date - x.DateFrom.Date).TotalDays <= 62)
            .When(x => x.DateFrom != default && x.DateTo != default && x.DateFrom <= x.DateTo)
            .WithMessage("El rango no puede superar los 62 días.");
    }
}
