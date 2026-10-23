using FluentValidation;

namespace eiti.Application.Features.Customers.Queries.GetCustomerLedger;

public sealed class GetCustomerLedgerValidator : AbstractValidator<GetCustomerLedgerQuery>
{
    public GetCustomerLedgerValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.DateFrom!.Value.Date <= x.DateTo!.Value.Date)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
    }
}
