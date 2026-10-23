using FluentValidation;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;

public sealed class GetSupplierLedgerValidator : AbstractValidator<GetSupplierLedgerQuery>
{
    public GetSupplierLedgerValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.DateFrom!.Value.Date <= x.DateTo!.Value.Date)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
    }
}
