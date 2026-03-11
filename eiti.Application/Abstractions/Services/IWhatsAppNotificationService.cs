namespace eiti.Application.Abstractions.Services;

public interface IWhatsAppNotificationService
{
    Task<WhatsAppSendResult> SendSalePaidMessageAsync(
        string fromPhone,
        string toPhone,
        string customerName,
        decimal totalAmount,
        string companyName,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSendResult(
    bool IsSuccess,
    string? ErrorMessage = null);
