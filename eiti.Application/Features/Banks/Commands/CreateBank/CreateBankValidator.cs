using FluentValidation;

namespace eiti.Application.Features.Banks.Commands.CreateBank;

public sealed class CreateBankValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
