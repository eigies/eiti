using System.Security.Cryptography;
using System.Text;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetHandler
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public RequestPasswordResetHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        // Always return success to not reveal whether the email exists
        if (!Email.IsValid(request.Email))
        {
            return Result.Success();
        }

        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var codeHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var token = PasswordResetToken.Create(
            user.Id.Value,
            codeHash,
            DateTime.UtcNow.AddMinutes(15));

        await _tokenRepository.AddAsync(token, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetCodeAsync(
            user.Email.Value,
            code,
            cancellationToken);

        return Result.Success();
    }
}
