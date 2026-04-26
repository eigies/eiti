using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Auth.Common;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Auth.Queries.Login;

public sealed class LoginHandler
    : IRequestHandler<LoginQuery, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICashDrawerRepository cashDrawerRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cashDrawerRepository = cashDrawerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginQuery request,
        CancellationToken cancellationToken)
    {
        User? user = null;

        if (Username.IsValid(request.UsernameOrEmail))
        {
            var username = Username.Create(request.UsernameOrEmail);
            user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        }

        if (user is null && Email.IsValid(request.UsernameOrEmail))
        {
            var email = Email.Create(request.UsernameOrEmail);
            user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        }

        if (user is null)
        {
            return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
        }

        bool isValidPassword = _passwordHasher.VerifyPassword(
            request.Password,
            user.PasswordHash.Value);

        if (!isValidPassword)
        {
            return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
        }

        user.UpdateLastLogin();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

        var assignedDrawer = await _cashDrawerRepository.GetByAssignedUserAsync(
            user.Id,
            new CompanyId(user.CompanyId.Value),
            cancellationToken);

        return Result<LoginResponse>.Success(
            new LoginResponse(
                user.Id.Value,
                user.Username.Value,
                user.Email.Value,
                token,
                roles,
                permissions,
                assignedDrawer?.Id.Value));
    }
}
