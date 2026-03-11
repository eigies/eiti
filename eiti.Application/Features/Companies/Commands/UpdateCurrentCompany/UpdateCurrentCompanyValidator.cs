using FluentValidation;

namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed class UpdateCurrentCompanyValidator : AbstractValidator<UpdateCurrentCompanyCommand>
{
    public UpdateCurrentCompanyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(100).WithMessage("Company name cannot exceed 100 characters.");

        RuleFor(x => x.PrimaryDomain)
            .NotEmpty().WithMessage("Company domain is required.")
            .MaximumLength(255).WithMessage("Company domain cannot exceed 255 characters.");

        RuleFor(x => x.WhatsAppSenderPhone)
            .MaximumLength(30).WithMessage("WhatsApp sender phone cannot exceed 30 characters.");

        RuleFor(x => x.WhatsAppSenderPhone)
            .NotEmpty().WithMessage("WhatsApp sender phone is required when WhatsApp is enabled.")
            .When(x => x.IsWhatsAppEnabled == true);
    }
}
