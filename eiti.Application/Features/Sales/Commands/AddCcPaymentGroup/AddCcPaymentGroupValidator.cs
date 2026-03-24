using FluentValidation;

namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public sealed class AddCcPaymentGroupValidator : AbstractValidator<AddCcPaymentGroupCommand>
{
    public AddCcPaymentGroupValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.Methods).NotEmpty();
        RuleForEach(x => x.Methods).ChildRules(line =>
        {
            line.RuleFor(l => l.IdPaymentMethod).InclusiveBetween(1, 5);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.CashDrawerId).NotEmpty();
    }
}
