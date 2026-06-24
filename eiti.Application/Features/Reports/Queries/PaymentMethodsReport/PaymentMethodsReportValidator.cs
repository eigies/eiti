using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.PaymentMethodsReport;

public sealed class PaymentMethodsReportValidator : AbstractValidator<PaymentMethodsReportQuery>
{
    public PaymentMethodsReportValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
    }
}
