using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
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

    public async Task<IReadOnlyList<CustomerPayment>> ListTransfersByDateAsync(
        Guid companyId,
        DateTime fromDate,
        DateTime toDate,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken ct = default)
    {
        // Date guarda el día elegido por el usuario (00:00 UTC); se compara por día calendario.
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toExclusive = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);
        var query = _db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId
                && p.Status == SaleCcPaymentStatus.Active
                && p.Method == SalePaymentMethod.Transfer
                && p.Date >= from
                && p.Date < toExclusive);

        if (branchId.HasValue)
        {
            var bId = branchId.Value;
            query = query.Where(p => p.BranchId == bId);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            var allowed = allowedBranchIds.ToList();
            query = query.Where(p => allowed.Contains(p.BranchId));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerPayment>> ListForPaymentMethodsReportAsync(
        Guid companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken ct = default)
    {
        var query = _db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId
                && p.Status == SaleCcPaymentStatus.Active
                && p.CreatedAt >= from
                && p.CreatedAt <= to);

        if (branchId.HasValue)
        {
            var bId = branchId.Value;
            query = query.Where(p => p.BranchId == bId);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            var allowed = allowedBranchIds.ToList();
            query = query.Where(p => allowed.Contains(p.BranchId));
        }

        return await query
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
    }
}
