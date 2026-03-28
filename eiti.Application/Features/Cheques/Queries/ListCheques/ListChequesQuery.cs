using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.ListCheques;

public sealed record ListChequesQuery(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo
) : IRequest<Result<IReadOnlyList<ChequeListItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ChequesManage];
}
