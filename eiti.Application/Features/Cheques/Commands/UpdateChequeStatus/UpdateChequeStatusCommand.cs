using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Cheques.Queries.GetChequeById;
using MediatR;

namespace eiti.Application.Features.Cheques.Commands.UpdateChequeStatus;

public sealed record UpdateChequeStatusCommand(Guid Id, int NewStatus) : IRequest<Result<ChequeDetailResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ChequesManage];
}
