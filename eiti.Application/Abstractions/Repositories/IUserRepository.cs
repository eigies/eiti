using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(Username username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(Username username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(Username username, UserId excludingUserId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(Email email, UserId excludingUserId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
