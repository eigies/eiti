using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.CreateCustomerCreditNote;

public sealed record CreateCustomerCreditNoteCommand(
    Guid CustomerId,
    decimal Amount,
    string Reason,
    DateTime Date,
    Guid? SaleId = null
) : IRequest<Result<CreateCustomerCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreditNoteCreate];
}
