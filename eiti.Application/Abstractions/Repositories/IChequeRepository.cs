using eiti.Domain.Cheques;

namespace eiti.Application.Abstractions.Repositories;

public record ChequeFilters(
    ChequeStatus? Estado,
    int? BankId,
    DateTime? FechaVencFrom,
    DateTime? FechaVencTo);

public interface IChequeRepository
{
    Task<IReadOnlyList<Cheque>> ListAsync(ChequeFilters filters, CancellationToken ct);
    Task<Cheque?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Cheque cheque, CancellationToken ct);
}
