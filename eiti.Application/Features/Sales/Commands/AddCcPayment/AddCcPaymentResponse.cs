namespace eiti.Application.Features.Sales.Commands.AddCcPayment;

public sealed record AddCcPaymentResponse(
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
