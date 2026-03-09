using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Auth.Commands.Register;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class RegisterHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateCompany_WhenDomainDoesNotExist()
    {
        var companyRepository = new Mock<ICompanyRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var userRepository = new Mock<IUserRepository>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepository
            .Setup(repository => repository.UsernameExistsAsync(
                It.IsAny<Username>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        userRepository
            .Setup(repository => repository.EmailExistsAsync(
                It.IsAny<eiti.Domain.Customers.Email>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        passwordHasher
            .Setup(service => service.HashPassword("password123"))
            .Returns("hashed-password");

        jwtTokenGenerator
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("jwt-token");

        var handler = new RegisterHandler(
            companyRepository.Object,
            companyOnboardingRepository.Object,
            userRepository.Object,
            passwordHasher.Object,
            jwtTokenGenerator.Object,
            unitOfWork.Object);

        var command = new RegisterCommand(
            "usuario",
            "user@acme.com",
            "password123",
            "Acme");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("jwt-token");

        companyRepository.Verify(repository => repository.AddAsync(
            It.Is<Company>(company =>
                company.Name.Value == "Acme" &&
                company.PrimaryDomain.Value.StartsWith("tenant-") &&
                company.PrimaryDomain.Value.EndsWith(".local")),
            It.IsAny<CancellationToken>()), Times.Once);

        companyOnboardingRepository.Verify(repository => repository.AddAsync(
            It.IsAny<CompanyOnboarding>(),
            It.IsAny<CancellationToken>()), Times.Once);

        userRepository.Verify(repository => repository.AddAsync(
            It.Is<User>(user => user.CompanyId != CompanyId.Empty),
            It.IsAny<CancellationToken>()), Times.Once);

        unitOfWork.Verify(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateNewCompany_EvenWhenEmailUsesPublicDomain()
    {
        var companyRepository = new Mock<ICompanyRepository>();
        var companyOnboardingRepository = new Mock<ICompanyOnboardingRepository>();
        var userRepository = new Mock<IUserRepository>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepository
            .Setup(repository => repository.UsernameExistsAsync(
                It.IsAny<Username>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        userRepository
            .Setup(repository => repository.EmailExistsAsync(
                It.IsAny<eiti.Domain.Customers.Email>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        passwordHasher
            .Setup(service => service.HashPassword("password123"))
            .Returns("hashed-password");

        jwtTokenGenerator
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("jwt-token");

        var handler = new RegisterHandler(
            companyRepository.Object,
            companyOnboardingRepository.Object,
            userRepository.Object,
            passwordHasher.Object,
            jwtTokenGenerator.Object,
            unitOfWork.Object);

        var command = new RegisterCommand(
            "usuario2",
            "otheruser@gmail.com",
            "password123",
            "Ohana");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        companyRepository.Verify(repository => repository.GetByPrimaryDomainAsync(
            It.IsAny<CompanyDomain>(),
            It.IsAny<CancellationToken>()), Times.Never);

        companyRepository.Verify(repository => repository.AddAsync(
            It.Is<Company>(company =>
                company.Name.Value == "Ohana" &&
                company.PrimaryDomain.Value.StartsWith("tenant-") &&
                company.PrimaryDomain.Value.EndsWith(".local")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
