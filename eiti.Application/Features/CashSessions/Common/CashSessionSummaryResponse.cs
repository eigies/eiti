namespace eiti.Application.Features.CashSessions.Common;

public sealed record CashSessionSummaryResponse(
    Guid Id,
    decimal OpeningAmount,
    decimal SalesIncome,
    decimal ManualDeposits,
    decimal Withdrawals,
    decimal SalesCancellations,
    decimal ExpectedClosingAmount,
    decimal? ActualClosingAmount,
    decimal Difference,
    IReadOnlyList<TransferBankBreakdownItem> TransferBankBreakdown);

public sealed record TransferBankBreakdownItem(int BankId, string BankName, decimal Amount);
