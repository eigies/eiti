using eiti.Domain.Employees;
using FluentValidation;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed class GeneratePayrollPeriodValidator : AbstractValidator<GeneratePayrollPeriodCommand>
{
    public GeneratePayrollPeriodValidator()
    {
        RuleFor(x => x.Periodicity).Must(value => Enum.IsDefined(typeof(PayrollPeriodicity), value));
        RuleFor(x => x.PeriodLabel).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PeriodEnd).GreaterThan(x => x.PeriodStart);
    }
}
