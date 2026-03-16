using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Auth.Queries.Login;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LoginHandler CreateHandler() =>
        new(_userRepository.Object, _passwordHasher.Object, _jwtTokenGenerator.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ShouldSearchByUsername_WhenInputIsValidUsername()
    {
        var user = User.Create(
            Username.Create("john"),
            Email.Create("john@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            ["owner"]);

        _userRepository
            .Setup(repository => repository.GetByUsernameAsync(
                It.Is<Username>(username => username.Value == "john"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(service => service.VerifyPassword("password123", "hashed"))
            .Returns(true);

        _jwtTokenGenerator
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("jwt-token");

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginQuery("john", "password123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _userRepository.Verify(
            repository => repository.GetByUsernameAsync(
                It.IsAny<Username>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSearchByEmail_WhenInputIsValidEmail()
    {
        var user = User.Create(
            Username.Create("john"),
            Email.Create("john@example.com"),
            PasswordHash.Create("hashed"),
            CompanyId.New(),
            ["owner"]);

        _userRepository
            .Setup(repository => repository.GetByUsernameAsync(
                It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepository
            .Setup(repository => repository.GetByEmailAsync(
                It.Is<Email>(email => email.Value == "john@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(service => service.VerifyPassword("password123", "hashed"))
            .Returns(true);

        _jwtTokenGenerator
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("jwt-token");

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginQuery("john@example.com", "password123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _userRepository.Verify(
            repository => repository.GetByEmailAsync(
                It.IsAny<Email>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInputIsInvalid()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(new LoginQuery("ab", "password123"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Auth.Login.InvalidCredentials");

        _userRepository.Verify(
            repository => repository.GetByUsernameAsync(
                It.IsAny<Username>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepository.Verify(
            repository => repository.GetByEmailAsync(
                It.IsAny<Email>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
