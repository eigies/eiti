using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class SaleFiscalDocumentRepository : ISaleFiscalDocumentRepository
{
    private readonly ApplicationDbContext _context;

    public SaleFiscalDocumentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SaleFiscalDocument document, CancellationToken cancellationToken = default)
    {
        await _context.SaleFiscalDocuments.AddAsync(document, cancellationToken);
    }

    public async Task<IReadOnlyList<SaleFiscalDocument>> ListBySaleAsync(
        SaleId saleId,
        CompanyId companyId,
        CancellationToken cancellationToken = default) =>
        await _context.SaleFiscalDocuments
            .Where(x => x.SaleId == saleId && x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    public Task<SaleFiscalDocument?> GetByRequestAsync(
        SaleId saleId,
        SaleFiscalDocumentKind kind,
        int sequence,
        CancellationToken cancellationToken = default) =>
        _context.SaleFiscalDocuments.FirstOrDefaultAsync(
            x => x.SaleId == saleId && x.Kind == kind && x.Sequence == sequence,
            cancellationToken);

    public Task<SaleFiscalDocument?> GetByIdAsync(
        SaleFiscalDocumentId id,
        CancellationToken cancellationToken = default) =>
        _context.SaleFiscalDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SaleFiscalDocument?> GetByFiscalDocumentIdAsync(
        Guid fiscalDocumentId,
        CancellationToken cancellationToken = default) =>
        _context.SaleFiscalDocuments.FirstOrDefaultAsync(
            x => x.FiscalDocumentId == fiscalDocumentId,
            cancellationToken);

    public async Task<IReadOnlyList<SaleFiscalDocument>> ListBySaleIdsAsync(
        IReadOnlyCollection<SaleId> saleIds,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        if (saleIds.Count == 0)
        {
            return [];
        }

        var ids = saleIds.Distinct().ToList();

        return await _context.SaleFiscalDocuments
            .Where(x => x.CompanyId == companyId && ids.Contains(x.SaleId))
            .ToListAsync(cancellationToken);
    }
}
