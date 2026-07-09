using FluentValidation;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public sealed class UpdateDeductionConceptValidator : AbstractValidator<UpdateDeductionConceptCommand>
{
    public UpdateDeductionConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
