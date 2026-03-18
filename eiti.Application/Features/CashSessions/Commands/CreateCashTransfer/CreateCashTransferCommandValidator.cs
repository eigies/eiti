using FluentValidation;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashTransfer;

public sealed class CreateCashTransferCommandValidator : AbstractValidator<CreateCashTransferCommand>
{
    public CreateCashTransferCommandValidator()
    {
        RuleFor(command => command.SourceCashDrawerId).NotEmpty();
        RuleFor(command => command.TargetCashDrawerId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(255);
    }
}
