using FluentValidation;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed class CreateBonusConceptValidator : AbstractValidator<CreateBonusConceptCommand>
{
    public CreateBonusConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
