using System.Net;
using eiti.Application.Abstractions.Services;
using eiti.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Resend;

namespace eiti.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private const string LogoUrl = "https://eiticloud.com/assets/brand/eiti-lockup-dark.png";

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
        var encodedCode = WebUtility.HtmlEncode(code);
        var message = new EmailMessage
        {
            From = $"{_settings.FromName} <{_settings.FromAddress}>",
            Subject = "Código de recuperación de contraseña - EITI",
            TextBody = $"""
                EITI | Recuperación de acceso

                Recibimos una solicitud para restablecer la contraseña de tu cuenta.

                Tu código de verificación es: {code}

                El código vence en 15 minutos y solo puede usarse una vez.
                Si no solicitaste este cambio, ignorá este email. Tu contraseña actual seguirá funcionando.

                Este es un mensaje automático. No respondas a este correo.
                """,
            HtmlBody = BuildPasswordResetHtml(encodedCode)
        };
        message.To.Add(toEmail);

        await _resend.EmailSendAsync(message, cancellationToken);
    }

    private static string BuildPasswordResetHtml(string code) =>
        $$"""
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="color-scheme" content="dark">
          <meta name="supported-color-schemes" content="dark">
          <title>Recuperación de contraseña</title>
          <style>
            @media only screen and (max-width: 620px) {
              .email-shell { width: 100% !important; }
              .email-padding { padding-left: 24px !important; padding-right: 24px !important; }
              .code-value { font-size: 36px !important; letter-spacing: 8px !important; }
            }
          </style>
        </head>
        <body style="margin:0;padding:0;background-color:#0a0a0f;color:#e8e0d0;">
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
            Tu código de recuperación EITI vence en 15 minutos.
          </div>
          <div style="display:none;max-height:0;overflow:hidden;">
            &#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;
          </div>

          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;background-color:#0a0a0f;">
            <tr>
              <td align="center" style="padding:40px 16px;">
                <!--[if mso]>
                <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0"><tr><td>
                <![endif]-->
                <table role="presentation" class="email-shell" width="600" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;background-color:#121219;border:1px solid #2a2a35;border-radius:20px;overflow:hidden;">
                  <tr>
                    <td height="5" style="height:5px;background-color:#f59e0b;font-size:0;line-height:0;">&nbsp;</td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:32px 40px 24px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                        <tr>
                          <td>
                            <a href="https://eiticloud.com" style="text-decoration:none;">
                              <img src="{{LogoUrl}}" width="252" alt="EITI cloud" border="0" style="display:block;width:252px;max-width:100%;height:auto;border:0;outline:none;text-decoration:none;">
                            </a>
                          </td>
                          <td align="right" style="font-family:'Courier New',Courier,monospace;font-size:10px;line-height:16px;letter-spacing:1.6px;color:#8f8777;text-transform:uppercase;">
                            Acceso seguro
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:12px 40px 0;">
                      <p style="margin:0 0 12px;font-family:'Courier New',Courier,monospace;font-size:11px;font-weight:700;line-height:18px;letter-spacing:2px;color:#f59e0b;text-transform:uppercase;">
                        _ Recuperación de acceso
                      </p>
                      <h1 style="margin:0 0 16px;font-family:Georgia,'Times New Roman',serif;font-size:32px;font-weight:400;line-height:39px;color:#f4eddf;">
                        Volvé a entrar al sistema.
                      </h1>
                      <p style="margin:0;font-family:Georgia,'Times New Roman',serif;font-size:17px;line-height:27px;color:#aaa08e;">
                        Recibimos una solicitud para restablecer la contraseña de tu cuenta. Ingresá este código en la pantalla de recuperación:
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:28px 40px 24px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#0c0c11;border:1px solid #34343f;border-radius:14px;">
                        <tr>
                          <td align="center" style="padding:20px 16px 8px;font-family:'Courier New',Courier,monospace;font-size:10px;font-weight:700;line-height:16px;letter-spacing:2px;color:#817866;text-transform:uppercase;">
                            Código de verificación
                          </td>
                        </tr>
                        <tr>
                          <td align="center" class="code-value" style="padding:0 12px 18px;font-family:'Courier New',Courier,monospace;font-size:42px;font-weight:700;line-height:52px;letter-spacing:12px;color:#f59e0b;white-space:nowrap;">
                            {{code}}
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:0 40px 28px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                        <tr>
                          <td width="12" valign="top" style="padding-top:5px;">
                            <div style="width:7px;height:7px;background-color:#f59e0b;border-radius:50%;font-size:0;line-height:0;">&nbsp;</div>
                          </td>
                          <td style="font-family:'Courier New',Courier,monospace;font-size:12px;line-height:20px;color:#c4baa8;">
                            Vence en <strong style="color:#f4eddf;">15 minutos</strong> y solo puede usarse una vez.
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:0 40px 36px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-left:3px solid #4a4439;background-color:#18171c;">
                        <tr>
                          <td style="padding:16px 18px;">
                            <p style="margin:0 0 5px;font-family:'Courier New',Courier,monospace;font-size:11px;font-weight:700;line-height:17px;letter-spacing:1.2px;color:#d8cebc;text-transform:uppercase;">
                              ¿No fuiste vos?
                            </p>
                            <p style="margin:0;font-family:Georgia,'Times New Roman',serif;font-size:14px;line-height:22px;color:#928a7c;">
                              Ignorá este email. Tu contraseña actual seguirá funcionando y no se realizará ningún cambio.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td class="email-padding" style="padding:22px 40px;background-color:#0d0d13;border-top:1px solid #24242d;">
                      <p style="margin:0;font-family:'Courier New',Courier,monospace;font-size:10px;line-height:17px;letter-spacing:.5px;color:#696356;">
                        EITI CLOUD &nbsp;/&nbsp; Mensaje automático, no respondas a este correo.
                      </p>
                    </td>
                  </tr>
                </table>
                <!--[if mso]>
                </td></tr></table>
                <![endif]-->
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
