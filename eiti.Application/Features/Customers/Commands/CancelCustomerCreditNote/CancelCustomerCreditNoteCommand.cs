using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CancelCustomerCreditNote;

public sealed record CancelCustomerCreditNoteCommand(Guid CustomerId, Guid CreditNoteId)
    : IRequest<Result<CancelCustomerCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreditNoteCancel];
}

public sealed record CancelCustomerCreditNoteResponse(Guid Id, string Code, decimal CustomerCreditBalance);
