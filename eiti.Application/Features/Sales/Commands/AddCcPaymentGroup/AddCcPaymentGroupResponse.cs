namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public sealed record AddCcPaymentGroupResponse(
    Guid GroupId,
    List<AddCcPaymentGroupItemResponse> Payments);

public sealed record AddCcPaymentGroupItemResponse(
    Guid Id, Guid SaleId, int IdPaymentMethod, string PaymentMethodName,
    decimal Amount, DateTime Date, string? Notes, int Status, string StatusName,
    DateTime CreatedAt, DateTime? CancelledAt, Guid? GroupId,
    int? CardBankId, int? CardCuotas, decimal? CardSurchargePct, decimal? CardSurchargeAmt, decimal? TotalCobrado);
