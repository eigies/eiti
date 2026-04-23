namespace eiti.Infrastructure.Settings;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string ApiKey { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}
