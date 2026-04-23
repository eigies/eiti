using eiti.Application.Abstractions.Services;
using eiti.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Resend;

namespace eiti.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly EmailSettings _settings;

    public EmailService(IResend resend, IOptions<EmailSettings> settings)
    {
        _resend = resend;
        _settings = settings.Value;
    }

    public async Task SendPasswordResetCodeAsync(
        string toEmail,
        string code,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = $"{_settings.FromName} <{_settings.FromAddress}>",
            Subject = "Código de recuperación de contraseña - EITI",
            HtmlBody = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:32px;">
                  <h2 style="margin-bottom:8px;">Recuperar contraseña</h2>
                  <p>Recibimos una solicitud para restablecer tu contraseña. Usá el siguiente código:</p>
                  <div style="font-size:36px;font-weight:bold;letter-spacing:8px;text-align:center;padding:24px;background:#f5f5f5;border-radius:8px;margin:24px 0;">
                    {code}
                  </div>
                  <p style="color:#666;font-size:14px;">Este código expira en <strong>15 minutos</strong>.</p>
                  <p style="color:#666;font-size:14px;">Si no solicitaste este cambio, podés ignorar este email.</p>
                </div>
                """
        };
        message.To.Add(toEmail);

        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
