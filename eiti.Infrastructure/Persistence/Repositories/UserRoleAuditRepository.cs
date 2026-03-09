using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class UserRoleAuditRepository : IUserRoleAuditRepository
{
    private readonly ApplicationDbContext _context;

    public UserRoleAuditRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserRoleAudit audit, CancellationToken cancellationToken = default)
    {
        await _context.UserRoleAudits.AddAsync(audit, cancellationToken);
    }

    public async Task<IReadOnlyList<UserRoleAudit>> ListByCompanyAsync(
        CompanyId companyId,
        UserId? targetUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserRoleAudits
            .AsNoTracking()
            .Where(audit => audit.CompanyId == companyId);

        if (targetUserId is not null)
        {
            query = query.Where(audit => audit.TargetUserId == targetUserId);
        }

        return await query
            .OrderByDescending(audit => audit.ChangedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
