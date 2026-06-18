using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

public sealed record AddCustomerPaymentResponse(
    Guid PaymentId,
    Guid CustomerId,
    decimal Amount,
    decimal AppliedToSales,
    decimal CreditAdded,
    decimal CustomerCreditBalance,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones);
