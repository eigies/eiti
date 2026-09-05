using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierCreditNote;

public sealed record CancelSupplierCreditNoteCommand(Guid SupplierId, Guid CreditNoteId)
    : IRequest<Result<CancelSupplierCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCreditNoteCancel];
}

public sealed record CancelSupplierCreditNoteResponse(Guid Id, string Code, decimal SupplierCreditBalance);
