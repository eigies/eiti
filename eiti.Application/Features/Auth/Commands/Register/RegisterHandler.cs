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
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterHandler(
        ICompanyRepository companyRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUserRepository userRepository,
        IAccessProfileRepository accessProfileRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _companyRepository = companyRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _userRepository = userRepository;
        _accessProfileRepository = accessProfileRepository;
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
        var accessProfiles = AccessProfileSeedCatalog.CreateInitialProfiles(company.Id);
        foreach (var profile in accessProfiles)
        {
            await _accessProfileRepository.AddAsync(profile, cancellationToken);
        }

        var ownerProfile = accessProfiles.First(profile => profile.SystemKey == SystemRoles.Owner);

        var user = User.Create(username, email, passwordHash, company.Id, ownerProfile);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var permissions = AuthenticationMapper.MapPermissions(user);

        return Result<RegisterResponse>.Success(
            new RegisterResponse(
                user.Id.Value,
                user.Username.Value,
                user.Email.Value,
                token,
                refreshToken,
                user.AccessProfileId.Value,
                ownerProfile.Name,
                permissions));
    }
}
