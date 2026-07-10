using FluentValidation;
using eiti.Domain.Payroll;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed class CreatePayrollAdvanceValidator : AbstractValidator<CreatePayrollAdvanceCommand>
{
    public CreatePayrollAdvanceValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).Must(value => Enum.IsDefined(typeof(PayrollPaymentMethod), value));
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.CashSessionId)
            .NotNull()
            .WithMessage("A cash session is required when paying in cash.")
            .When(x => x.PaymentMethod == (int)PayrollPaymentMethod.Cash);
    }
}
