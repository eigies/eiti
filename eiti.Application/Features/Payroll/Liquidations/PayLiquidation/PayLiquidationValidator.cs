using eiti.Domain.Payroll;
using FluentValidation;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed class PayLiquidationValidator : AbstractValidator<PayLiquidationCommand>
{
    public PayLiquidationValidator()
    {
        RuleFor(x => x.LiquidationId).NotEmpty();
        RuleFor(x => x.PaymentMethod).Must(value => Enum.IsDefined(typeof(PayrollPaymentMethod), value));
        RuleFor(x => x.CashSessionId)
            .NotNull()
            .WithMessage("A cash session is required when paying in cash.")
            .When(x => x.PaymentMethod == (int)PayrollPaymentMethod.Cash);
    }
}
