namespace eiti.Application.Features.Reports.Queries.CustomerDebtors;

public sealed record CustomerDebtorsResponse(
    IReadOnlyList<CustomerDebtorRow> Rows,
    decimal TotalOwed,
    decimal TotalCredit,
    decimal TotalNet);

public sealed record CustomerDebtorRow(
    Guid CustomerId,
    string CustomerName,
    int OpenSalesCount,
    DateTime OldestDate,
    decimal Owed,
    decimal CreditBalance,
    decimal Net);
