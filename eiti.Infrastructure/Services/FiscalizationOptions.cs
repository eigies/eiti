namespace eiti.Infrastructure.Services;

public sealed class FiscalizationOptions
{
    public const string SectionName = "Fiscalization";

    /// <summary>Base del servicio fiscal, ej. https://fiscal.eiticloud.com</summary>
    public string? BaseUrl { get; init; }

    /// <summary>API key de servicio. Viaja en el header X-Api-Key. Nunca hardcodear: env var.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Régimen fiscal del tenant. Discriminador del contrato multi-régimen.</summary>
    public string Regime { get; init; } = "AR-ARCA";

    /// <summary>Punto de venta por defecto cuando la sucursal no define uno.</summary>
    public int DefaultPointOfSale { get; init; } = 1;

    /// <summary>URL pública del callback de EITI que recibe la resolución diferida.</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>Secreto compartido con el que el servicio fiscal firma el callback.</summary>
    public string? CallbackSigningSecret { get; init; }

    /// <summary>Secreto anterior, para poder rotar sin downtime.</summary>
    public string? CallbackSigningSecretPrevious { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}
