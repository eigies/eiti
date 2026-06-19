using eiti.Domain.Primitives;

namespace eiti.Domain.Companies;

public sealed class Company : AggregateRoot<CompanyId>
{
    public CompanyName Name { get; private set; }
    public CompanyDomain PrimaryDomain { get; private set; }
    public bool IsWhatsAppEnabled { get; private set; }
    public string? WhatsAppSenderPhone { get; private set; }
    public decimal? DefaultNoDeliverySurcharge { get; private set; }
    public string? PdfLogoUrl { get; private set; }
    public string? PdfWatermarkUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Company()
    {
    }

    private Company(
        CompanyId id,
        CompanyName name,
        CompanyDomain primaryDomain,
        bool isWhatsAppEnabled,
        string? whatsAppSenderPhone,
        string? pdfLogoUrl,
        string? pdfWatermarkUrl,
        DateTime createdAt)
        : base(id)
    {
        Name = name;
        PrimaryDomain = primaryDomain;
        IsWhatsAppEnabled = isWhatsAppEnabled;
        WhatsAppSenderPhone = whatsAppSenderPhone;
        PdfLogoUrl = pdfLogoUrl;
        PdfWatermarkUrl = pdfWatermarkUrl;
        CreatedAt = createdAt;
    }

    public static Company Create(
        CompanyName name,
        CompanyDomain primaryDomain)
    {
        return new Company(
            CompanyId.New(),
            name,
            primaryDomain,
            false,
            null,
            null,
            null,
            DateTime.UtcNow);
    }

    public static Company CreateLegacy(CompanyId id)
    {
        return new Company(
            id,
            CompanyName.Create("Legacy Company"),
            CompanyDomain.Create("legacy.local"),
            false,
            null,
            null,
            null,
            DateTime.UtcNow);
    }

    public void Update(
        CompanyName name,
        CompanyDomain primaryDomain,
        bool isWhatsAppEnabled,
        string? whatsAppSenderPhone,
        decimal? defaultNoDeliverySurcharge = null,
        string? pdfLogoUrl = null,
        string? pdfWatermarkUrl = null)
    {
        var normalizedSenderPhone = NormalizeSenderPhone(whatsAppSenderPhone);
        if (isWhatsAppEnabled && string.IsNullOrWhiteSpace(normalizedSenderPhone))
        {
            throw new ArgumentException(
                "WhatsApp sender phone is required when WhatsApp is enabled.",
                nameof(whatsAppSenderPhone));
        }

        Name = name;
        PrimaryDomain = primaryDomain;
        IsWhatsAppEnabled = isWhatsAppEnabled;
        WhatsAppSenderPhone = normalizedSenderPhone;
        DefaultNoDeliverySurcharge = defaultNoDeliverySurcharge;
        PdfLogoUrl = NormalizeOptional(pdfLogoUrl, 1_000_000, nameof(pdfLogoUrl));
        PdfWatermarkUrl = NormalizeOptional(pdfWatermarkUrl, 1_000_000, nameof(pdfWatermarkUrl));
    }

    private static string? NormalizeSenderPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > 30)
        {
            throw new ArgumentException("WhatsApp sender phone cannot exceed 30 characters.", nameof(value));
        }

        return normalizedValue;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
        }

        return normalized;
    }
}
