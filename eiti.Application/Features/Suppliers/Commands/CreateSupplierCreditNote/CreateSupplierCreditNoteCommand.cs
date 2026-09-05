using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Purchases.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CreateSupplierCreditNote;

public sealed record CreateSupplierCreditNoteCommand(
    Guid SupplierId,
    decimal Amount,
    string Reason,
    DateTime Date,
    Guid? PurchaseId = null
) : IRequest<Result<CreateSupplierCreditNoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCreditNoteCreate];
}

public sealed record CreateSupplierCreditNoteResponse(
    Guid Id,
    string Code,
    decimal Amount,
    decimal SupplierCreditBalance,
    IReadOnlyList<SupplierPaymentImputacion> Imputaciones,
    decimal Sobrante);
