using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Banks.Queries.ListBanks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.CreateBank;

public sealed record CreateBankCommand(string Name) : IRequest<Result<BankResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.BanksManage];
}
