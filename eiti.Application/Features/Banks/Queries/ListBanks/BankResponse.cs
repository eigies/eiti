namespace eiti.Application.Features.Banks.Queries.ListBanks;

public sealed record BankInstallmentPlanResponse(int Id, int Cuotas, decimal SurchargePct, bool Active);

public sealed record BankResponse(int Id, string Name, bool Active, IReadOnlyList<BankInstallmentPlanResponse> Plans);
