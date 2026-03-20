using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Auth.Common;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.Register;

public sealed class RegisterHandler
    : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterHandler(
        ICompanyRepository companyRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _companyRepository = companyRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        Username username;
        Email email;
        CompanyName companyName;
        CompanyDomain companyDomain;

        try
        {
            username = Username.Create(request.Username);
            email = Email.Create(request.Email);
            companyName = CompanyName.Create(request.CompanyName);
            companyDomain = CompanyDomain.Create($"tenant-{Guid.NewGuid():N}.local");
        }
        catch (ArgumentException ex)
        {
            return Result<RegisterResponse>.Failure(
                Error.Validation("Auth.Register.InvalidInput", ex.Message));
        }

        if (await _userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            return Result<RegisterResponse>.Failure(RegisterErrors.UsernameAlreadyExists);
        }

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            return Result<RegisterResponse>.Failure(RegisterErrors.EmailAlreadyExists);
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);
        var passwordHash = PasswordHash.Create(hashedPassword);

        var company = Company.Create(companyName, companyDomain);
        await _companyRepository.AddAsync(company, cancellationToken);
        await _companyOnboardingRepository.AddAsync(CompanyOnboarding.CreateIncomplete(company.Id), cancellationToken);

        var user = User.Create(username, email, passwordHash, company.Id, [SystemRoles.Owner]);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

        return Result<RegisterResponse>.Success(
            new RegisterResponse(
                user.Id.Value,
                user.Username.Value,
                user.Email.Value,
                token,
                roles,
                permissions));
    }
}
