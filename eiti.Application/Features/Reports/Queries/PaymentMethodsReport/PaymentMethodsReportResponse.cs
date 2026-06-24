namespace eiti.Application.Features.Reports.Queries.PaymentMethodsReport;

public sealed record PaymentMethodsReportResponse(
    IReadOnlyList<PaymentMethodsReportRow> Rows,
    PaymentMethodsReportTotals Totals);

public sealed record PaymentMethodsReportRow(
    int MethodId,
    string MethodLabel,
    int Count,
    decimal Total,
    decimal Percent,
    IReadOnlyList<PaymentMethodsReportSubgroup> Subgroups);

// Desglose de un medio de pago (hoy: Tarjeta por banco + cuotas). Percent sobre el total general.
public sealed record PaymentMethodsReportSubgroup(
    string Label,
    int? CardBankId,
    int? CardCuotas,
    int Count,
    decimal Total,
    decimal Percent);

public sealed record PaymentMethodsReportTotals(
    int Count,
    decimal Total);
