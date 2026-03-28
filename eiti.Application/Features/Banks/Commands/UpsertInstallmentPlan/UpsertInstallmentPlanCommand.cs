using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Banks.Queries.ListBanks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.UpsertInstallmentPlan;

public sealed record UpsertInstallmentPlanCommand(int BankId, int Cuotas, decimal SurchargePct, bool Active) : IRequest<Result<BankResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.BanksManage];
}
