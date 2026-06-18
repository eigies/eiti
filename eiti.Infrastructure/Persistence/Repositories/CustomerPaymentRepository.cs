using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class CustomerPaymentRepository : ICustomerPaymentRepository
{
    private readonly ApplicationDbContext _db;

    public CustomerPaymentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerPayment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await _db.CustomerPayments
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, ct);
    }

    public async Task<List<CustomerPayment>> ListByCustomerAsync(Guid companyId, Guid customerId, CancellationToken ct = default)
    {
        return await _db.CustomerPayments
            .Where(p => p.CompanyId == companyId && p.CustomerId == customerId)
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CustomerPayment payment, CancellationToken ct = default)
    {
        await _db.CustomerPayments.AddAsync(payment, ct);
    }
}
