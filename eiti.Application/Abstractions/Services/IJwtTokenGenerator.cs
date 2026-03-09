using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
