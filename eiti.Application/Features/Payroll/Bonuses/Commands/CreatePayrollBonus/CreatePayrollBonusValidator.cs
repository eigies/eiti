using FluentValidation;
using eiti.Domain.Payroll;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed class CreatePayrollBonusValidator : AbstractValidator<CreatePayrollBonusCommand>
{
    public CreatePayrollBonusValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.ConceptId).NotEmpty();
        RuleFor(x => x.AmountType).Must(value => Enum.IsDefined(typeof(PayrollBonusAmountType), value));
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .WithMessage("Percentage value must be between 0 and 100.")
            .When(x => x.AmountType == (int)PayrollBonusAmountType.Percentage);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
