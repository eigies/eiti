using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Banks.Queries.ListBanks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.UpdateBank;

public sealed record UpdateBankCommand(int Id, string Name, bool Active) : IRequest<Result<BankResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.BanksManage];
}
