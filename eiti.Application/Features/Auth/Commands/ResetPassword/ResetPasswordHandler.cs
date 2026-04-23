using System.Security.Cryptography;
using System.Text;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordHandler
    : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ResetPasswordHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!Email.IsValid(request.Email))
        {
            return Result.Failure(ResetPasswordErrors.InvalidOrExpiredCode);
        }

        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result.Failure(ResetPasswordErrors.InvalidOrExpiredCode);
        }

        var token = await _tokenRepository.GetActiveByUserIdAsync(user.Id.Value, cancellationToken);

        if (token is null)
        {
            return Result.Failure(ResetPasswordErrors.InvalidOrExpiredCode);
        }

        var incomingHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Code.Trim())));

        if (!string.Equals(token.CodeHash, incomingHash, StringComparison.Ordinal))
        {
            return Result.Failure(ResetPasswordErrors.InvalidOrExpiredCode);
        }

        var hashedPassword = _passwordHasher.HashPassword(request.NewPassword);
        var newPasswordHash = PasswordHash.Create(hashedPassword);

        user.ChangePassword(newPasswordHash);
        token.MarkAsUsed();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
