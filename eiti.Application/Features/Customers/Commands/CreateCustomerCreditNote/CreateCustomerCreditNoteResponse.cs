using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public sealed record CreateCustomerCreditNoteResponse(
    Guid Id,
    string Code,
    decimal Amount,
    decimal CustomerCreditBalance,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones,
    decimal Sobrante);
