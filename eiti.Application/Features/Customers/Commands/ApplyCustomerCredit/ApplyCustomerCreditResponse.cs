using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.ApplyCustomerCredit;

public sealed record ApplyCustomerCreditResponse(
    Guid CustomerId,
    decimal Applied,
    decimal RemainingCredit,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones);
