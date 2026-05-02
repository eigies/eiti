using eiti.Domain.Cheques;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public record ChequeFilters(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo);

public interface IChequeRepository
{
    Task<IReadOnlyList<Cheque>> ListAsync(ChequeFilters filters, CompanyId companyId, CancellationToken ct);
    Task<Cheque?> GetByIdAsync(Guid id, CompanyId companyId, CancellationToken ct);
    Task AddAsync(Cheque cheque, CancellationToken ct);
}
