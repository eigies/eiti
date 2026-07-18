using eiti.Domain.Companies;
using eiti.Domain.Quotes;

namespace eiti.Application.Abstractions.Repositories;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken = default);

    Task<Quote?> GetByIdAsync(
        QuoteId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Quote>> ListAsync(
        CompanyId companyId,
        QuoteStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        Guid? customerId,
        CancellationToken cancellationToken = default);

    Task<int> CountByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}
