using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.GetChequeById;

public sealed record GetChequeByIdQuery(Guid Id) : IRequest<Result<ChequeDetailResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ChequesManage];
}
