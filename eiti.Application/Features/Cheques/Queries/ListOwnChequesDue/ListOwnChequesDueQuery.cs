using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.ListOwnChequesDue;

// Cheques propios que vencen hoy: lo que se debita del banco y hay que avisar al entrar a EITI.
public sealed record ListOwnChequesDueQuery
    : IRequest<Result<IReadOnlyList<OwnChequeDueResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ChequesManage];
}
