namespace eiti.Application.Features.CashSessions.Common;

public record PaymentMethodBreakdownItem(
    int Method,
    string MethodName,
    decimal Amount,
    decimal SurchargeAmount = 0m);
