using FluentValidation;

namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed class SetEmployeePayrollConfigValidator : AbstractValidator<SetEmployeePayrollConfigCommand>
{
    public SetEmployeePayrollConfigValidator()
    {
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0).When(x => x.BaseSalary.HasValue);
        RuleFor(x => x.PayrollPeriodicity)
            .Must(value => Enum.IsDefined(typeof(eiti.Domain.Employees.PayrollPeriodicity), value!.Value))
            .When(x => x.PayrollPeriodicity.HasValue)
            .WithMessage("The selected payroll periodicity is invalid.");
    }
}
