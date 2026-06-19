using FluentValidation;

namespace eiti.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("El email debe tener un formato valido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.FirstName))
            .WithMessage("Debe informar nombre o nombre y apellido.");

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.DocumentNumber));

        RuleFor(x => x.TaxId)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

    }
}
