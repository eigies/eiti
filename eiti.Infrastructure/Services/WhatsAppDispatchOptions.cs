namespace eiti.Infrastructure.Services;

public sealed class WhatsAppDispatchOptions
{
    public const string SectionName = "WhatsAppDispatch";

    public string? DispatchUrl { get; init; }
    public string? ApiKey { get; init; }
}
