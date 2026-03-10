using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(
        UserId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(
        Username username,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .Include(u => u.Roles)
            .ToListAsync(cancellationToken);

        return users.FirstOrDefault(u => u.Username.Value == username.Value);
    }

    public async Task<User?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .Include(u => u.Roles)
            .ToListAsync(cancellationToken);

        return users.FirstOrDefault(u => u.Email.Value == email.Value);
    }

    public async Task<IReadOnlyList<User>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(
        Username username,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users.ToListAsync(cancellationToken);
        return users.Any(u => u.Username.Value == username.Value);
    }

    public async Task<bool> EmailExistsAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users.ToListAsync(cancellationToken);
        return users.Any(u => u.Email.Value == email.Value);
    }

    public async Task<bool> UsernameExistsAsync(
        Username username,
        UserId excludingUserId,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users.ToListAsync(cancellationToken);
        return users.Any(u => u.Username.Value == username.Value && u.Id != excludingUserId);
    }

    public async Task<bool> EmailExistsAsync(
        Email email,
        UserId excludingUserId,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users.ToListAsync(cancellationToken);
        return users.Any(u => u.Email.Value == email.Value && u.Id != excludingUserId);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }
}
