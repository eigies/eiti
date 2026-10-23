using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.AddCustomerReceipt;

public sealed record AddCustomerReceiptResponse(
    Guid ReceiptId,
    string Code,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<Guid> PaymentIds,
    decimal AppliedToSales,
    decimal CreditAdded,
    decimal CustomerCreditBalance,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones);
