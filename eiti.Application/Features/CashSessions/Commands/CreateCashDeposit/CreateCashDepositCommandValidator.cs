using FluentValidation;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashDeposit;

public sealed class CreateCashDepositCommandValidator : AbstractValidator<CreateCashDepositCommand>
{
    public CreateCashDepositCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(255);
    }
}
