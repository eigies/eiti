using FluentValidation;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed class CreateDeductionConceptValidator : AbstractValidator<CreateDeductionConceptCommand>
{
    public CreateDeductionConceptValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
