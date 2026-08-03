using FluentValidation;

namespace eiti.Application.Features.Reports.Queries.TradeInsByBranch;

public sealed class TradeInsByBranchValidator : AbstractValidator<TradeInsByBranchQuery>
{
    public TradeInsByBranchValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEmpty().WithMessage("La fecha desde es obligatoria.");

        RuleFor(x => x.DateTo)
            .NotEmpty().WithMessage("La fecha hasta es obligatoria.")
            .GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha hasta no puede ser anterior a la fecha desde.");
    }
}
