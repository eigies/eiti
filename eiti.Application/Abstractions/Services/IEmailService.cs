namespace eiti.Application.Abstractions.Services;

public interface IEmailService
{
    Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken = default);
}
