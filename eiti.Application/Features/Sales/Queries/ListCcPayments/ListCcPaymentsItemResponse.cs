namespace eiti.Application.Features.Sales.Queries.ListCcPayments;

public sealed record ListCcPaymentsItemResponse(
    Guid Id,
    Guid SaleId,
    int IdPaymentMethod,
    string PaymentMethodName,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int Status,
    string StatusName,
    DateTime CreatedAt,
    DateTime? CancelledAt);
