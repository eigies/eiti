using eiti.Domain.Branches;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(
        BranchId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        BranchId? excludedId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default);

    // True si la sucursal tiene actividad que impide su borrado físico:
    // ventas, cajas, movimientos de stock, usuarios asignados, o stock con cantidad > 0.
    Task<bool> IsReferencedAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    // Borrado físico: limpia las filas de stock vacías (contadores en 0) de la sucursal
    // — que solo bloquean la FK — y remueve la entidad.
    Task DeleteAsync(
        Branch branch,
        CancellationToken cancellationToken = default);
}
