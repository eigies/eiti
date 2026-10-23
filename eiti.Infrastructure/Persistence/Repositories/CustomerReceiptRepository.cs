using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CustomerReceiptRepository : ICustomerReceiptRepository
{
    private readonly ApplicationDbContext _db;

    public CustomerReceiptRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerReceipt?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.CustomerReceipts
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);
    }

    public async Task<int> CountByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _db.CustomerReceipts
            .CountAsync(r => r.CompanyId == companyId, ct);
    }

    public async Task AddAsync(CustomerReceipt receipt, CancellationToken ct = default)
    {
        await _db.CustomerReceipts.AddAsync(receipt, ct);
    }
}
