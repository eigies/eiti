namespace eiti.Application.Common.Accounts;

// Extracto de cuenta corriente (proveedor o cliente): mismo contrato para los dos, así el front usa un solo
// componente. Saldo positivo = se debe; negativo = saldo a favor.
public sealed record AccountLedgerResponse(
    Guid AccountId,
    string AccountName,
    DateTime? DateFrom,
    DateTime? DateTo,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    IReadOnlyList<LedgerRowResponse> Rows,
    IReadOnlyList<PendingDocumentResponse> Pending);

// Kind: qué es ("invoice" | "payment" | "credit_note"). Source: de dónde sale y, con eso, cómo se anula:
// "purchase" | "sale" | "payment_order" | "receipt" | "payment" (pago sin grupo, anterior a las órdenes) |
// "legacy" (pago cargado en la compra/venta antes de la cuenta corriente) | "credit_note".
// Id es el de la cosa que se anula: la orden / el recibo para los grupos.
public sealed record LedgerRowResponse(
    string Kind,
    string Source,
    Guid Id,
    DateTime Date,
    string? Code,
    string? Document,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    bool IsCancelled,
    IReadOnlyList<LedgerMeansResponse> Means,
    IReadOnlyList<LedgerImputationResponse> Imputations);

// Un medio dentro de un pago/cobro. Method: "cash" | "transfer" | "check" | "card" | "other".
public sealed record LedgerMeansResponse(
    Guid PaymentId,
    string Method,
    string MethodLabel,
    decimal Amount,
    string? Reference,
    string? ChequeNumber = null,
    string? BankName = null,
    DateTime? DueDate = null,
    int? CardInstallments = null,
    decimal? CardSurcharge = null);

// A qué factura/venta se aplicó (parte de) un pago, cobro o nota de crédito.
public sealed record LedgerImputationResponse(
    Guid DocumentId,
    string Code,
    string? Document,
    decimal Amount);

// Factura/venta con saldo, para la pestaña Pendientes.
public sealed record PendingDocumentResponse(
    Guid Id,
    DateTime Date,
    string Code,
    string? Document,
    decimal Total,
    decimal Imputed,
    decimal Pending,
    int AgeDays);
