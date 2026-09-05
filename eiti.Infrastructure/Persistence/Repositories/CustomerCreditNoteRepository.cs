using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CustomerCreditNoteRepository : ICustomerCreditNoteRepository
{
    private readonly ApplicationDbContext _db;

    public CustomerCreditNoteRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerCreditNote?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.CompanyId == companyId, ct);
    }

    public async Task<List<CustomerCreditNote>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId)
            .OrderByDescending(n => n.Date)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> CountByBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        return await _db.CustomerCreditNotes
            .CountAsync(n => n.CompanyId == companyId && n.BranchId == branchId, ct);
    }

    public async Task AddAsync(CustomerCreditNote note, CancellationToken ct = default)
    {
        await _db.CustomerCreditNotes.AddAsync(note, ct);
    }
}
