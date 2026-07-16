using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly ApplicationDbContext _context;

    public QuoteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        await _context.Quotes.AddAsync(quote, cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(
        QuoteId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Quotes
            .Include(quote => quote.Details)
            .FirstOrDefaultAsync(quote => quote.Id == id && quote.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<Quote>> ListAsync(
        CompanyId companyId,
        QuoteStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Quotes
            .Include(quote => quote.Details)
            .AsNoTracking()
            .Where(quote => quote.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(quote => quote.Status == status.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(quote => quote.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(quote => quote.CreatedAt <= dateTo.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(quote => quote.CustomerId != null && quote.CustomerId.Value == customerId.Value);
        }

        return await query
            .OrderByDescending(quote => quote.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Quotes.CountAsync(quote => quote.BranchId.Value == branchId, cancellationToken);
    }
}
