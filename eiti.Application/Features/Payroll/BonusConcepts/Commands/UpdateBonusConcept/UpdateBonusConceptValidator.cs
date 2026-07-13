using FluentValidation;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public sealed class UpdateBonusConceptValidator : AbstractValidator<UpdateBonusConceptCommand>
{
    public UpdateBonusConceptValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
