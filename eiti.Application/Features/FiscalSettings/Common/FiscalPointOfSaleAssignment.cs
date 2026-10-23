using eiti.Application.Abstractions.Repositories;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;

namespace eiti.Application.Features.FiscalSettings.Common;

/// <summary>
/// Ata un punto de venta a una sucursal (1:1) o lo desata (branchId null). Si ya estaba en otra
/// sucursal se mueve: esa deja de poder facturar hasta que le asignen otro. A una sucursal que ya
/// factura con otro punto de venta no se le pisa: primero hay que sacárselo.
/// </summary>
public static class FiscalPointOfSaleAssignment
{
    public static async Task<Result<Branch?>> ApplyAsync(
        IBranchRepository branches,
        FiscalPointOfSale pointOfSale,
        Guid? branchId,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var current = await branches.GetByFiscalPointOfSaleIdAsync(pointOfSale.Id, companyId, cancellationToken);
        if (branchId is null)
        {
            current?.ClearFiscalPointOfSale();
            return Result<Branch?>.Success(null);
        }

        if (current?.Id.Value == branchId)
        {
            return Result<Branch?>.Success(current);
        }

        var target = await branches.GetByIdAsync(new BranchId(branchId.Value), companyId, cancellationToken);
        if (target is null)
        {
            return Result<Branch?>.Failure(FiscalPointOfSaleErrors.BranchNotFound);
        }

        if (target.FiscalPointOfSale is not null)
        {
            return Result<Branch?>.Failure(FiscalPointOfSaleErrors.BranchHasAnother(target.Name, target.FiscalPointOfSale.Number));
        }

        current?.ClearFiscalPointOfSale();
        target.AssignFiscalPointOfSale(pointOfSale);
        return Result<Branch?>.Success(target);
    }
}
