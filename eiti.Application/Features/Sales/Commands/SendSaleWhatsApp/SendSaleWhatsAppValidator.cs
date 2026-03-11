using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;

public sealed class SendSaleWhatsAppValidator : AbstractValidator<SendSaleWhatsAppCommand>
{
    public SendSaleWhatsAppValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Sale id is required.");
    }
}

