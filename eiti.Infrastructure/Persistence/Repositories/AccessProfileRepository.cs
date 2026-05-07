using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class AccessProfileRepository : IAccessProfileRepository
{
    private readonly ApplicationDbContext _context;

    public AccessProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AccessProfile?> GetByIdAsync(AccessProfileId id, CancellationToken cancellationToken = default)
    {
        return await _context.AccessProfiles
            .Include(profile => profile.Permissions)
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
    }

    public async Task<AccessProfile?> GetBySystemKeyAsync(CompanyId companyId, string systemKey, CancellationToken cancellationToken = default)
    {
        var normalizedSystemKey = systemKey.Trim().ToLowerInvariant();

        return await _context.AccessProfiles
            .Include(profile => profile.Permissions)
            .FirstOrDefaultAsync(
                profile => profile.CompanyId == companyId && profile.SystemKey == normalizedSystemKey,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AccessProfile>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default)
    {
        return await _context.AccessProfiles
            .Include(profile => profile.Permissions)
            .Where(profile => profile.CompanyId == companyId)
            .OrderBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(CompanyId companyId, string name, AccessProfileId? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        return await _context.AccessProfiles.AnyAsync(
            profile => profile.CompanyId == companyId
                && profile.Name == normalizedName
                && (excludingId == null || profile.Id != excludingId),
            cancellationToken);
    }

    public async Task<bool> HasUsersAssignedAsync(AccessProfileId id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(user => user.AccessProfileId == id, cancellationToken);
    }

    public async Task AddAsync(AccessProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.AccessProfiles.AddAsync(profile, cancellationToken);
    }

    public void Remove(AccessProfile profile)
    {
        _context.AccessProfiles.Remove(profile);
    }
}
