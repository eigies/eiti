using eiti.Domain.Customers;
using FluentValidation;

namespace eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;

public sealed class UpdateFiscalIssuerValidator : AbstractValidator<UpdateFiscalIssuerCommand>
{
    public UpdateFiscalIssuerValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().WithMessage("La razón social es obligatoria.").MaximumLength(250);
        // Quien factura es Responsable Inscripto, Monotributista o Exento; nunca Consumidor Final.
        RuleFor(x => x.IvaCondition)
            .Must(condition => Enum.IsDefined(typeof(IvaCondition), condition) && condition != IvaCondition.ConsumidorFinal)
            .WithMessage("La condición frente al IVA tiene que ser Responsable Inscripto, Monotributo o Exento.");
        RuleFor(x => x.Iibb).MaximumLength(32);
        RuleFor(x => x.CommercialAddress).MaximumLength(250);
        RuleFor(x => x.ActivityStartDate)
            .Must(date => date != default && date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de inicio de actividades no puede ser futura.");
    }
}
