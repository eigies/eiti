using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.ListAuditLog;

public sealed class ListAuditLogValidator : AbstractValidator<ListAuditLogQuery>
{
    public ListAuditLogValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");

        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");

        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");

        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
