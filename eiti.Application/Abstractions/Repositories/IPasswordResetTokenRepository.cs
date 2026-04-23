using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
