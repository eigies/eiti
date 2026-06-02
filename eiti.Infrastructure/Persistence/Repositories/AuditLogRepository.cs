using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Audit;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditLog>> ListAsync(
        CompanyId companyId,
        UserId? userId,
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, userId, from, to)
            .OrderByDescending(audit => audit.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        CompanyId companyId,
        UserId? userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(companyId, userId, from, to).CountAsync(cancellationToken);
    }

    private IQueryable<AuditLog> BuildQuery(
        CompanyId companyId,
        UserId? userId,
        DateTime from,
        DateTime to)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(audit => audit.CompanyId == companyId)
            .Where(audit => audit.Timestamp >= from && audit.Timestamp <= to);

        if (userId is not null)
        {
            query = query.Where(audit => audit.UserId == userId);
        }

        return query;
    }
}
