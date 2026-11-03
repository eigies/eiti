using eiti.Domain.Cheques;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public record ChequeFilters(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo,
    string? Numero = null,
    ChequeOrigen? Origen = null);

public interface IChequeRepository
{
    Task<IReadOnlyList<Cheque>> ListAsync(ChequeFilters filters, CompanyId companyId, CancellationToken ct);
    Task<Cheque?> GetByIdAsync(Guid id, CompanyId companyId, CancellationToken ct);
    Task<IReadOnlyList<Cheque>> ListByIdsAsync(IEnumerable<Guid> ids, CompanyId companyId, CancellationToken ct);
    // Un cheque propio no anulado con ese número en ese banco (anulado, el número se puede volver a usar).
    Task<bool> ExistsActiveOwnAsync(CompanyId companyId, int bankId, string numero, CancellationToken ct);
    Task AddAsync(Cheque cheque, CancellationToken ct);
}
