using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using eiti.Application.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace eiti.Infrastructure.Services;

/// <summary>
/// Espejo exacto del firmante del servicio fiscal: HMAC-SHA256 sobre "{timestamp}.{rawBody}",
/// hex en minúsculas, con prefijo "sha256=". Ventana anti-replay de 5 minutos.
/// </summary>
public sealed class FiscalCallbackSignatureValidator : IFiscalCallbackSignatureValidator
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);
    private const string Prefix = "sha256=";

    private readonly IOptions<FiscalizationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public FiscalCallbackSignatureValidator(IOptions<FiscalizationOptions> options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public bool IsValid(ReadOnlySpan<byte> rawBody, string? timestampHeader, string? signatureHeader)
    {
        var secret = _options.Value.CallbackSigningSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Sin secreto configurado no se puede verificar nada: se rechaza. Nunca "pasar igual".
            return false;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader) ||
            !signatureHeader.StartsWith(Prefix, StringComparison.Ordinal) ||
            !long.TryParse(timestampHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if ((_timeProvider.GetUtcNow() - signedAt).Duration() > MaximumAge)
        {
            return false;
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signatureHeader[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (Matches(secret, rawBody, timestamp, supplied))
        {
            return true;
        }

        var previous = _options.Value.CallbackSigningSecretPrevious;
        return !string.IsNullOrWhiteSpace(previous) && Matches(previous, rawBody, timestamp, supplied);
    }

    private static bool Matches(string secret, ReadOnlySpan<byte> body, long timestamp, byte[] supplied)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0);
        body.CopyTo(input.AsSpan(prefix.Length));

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
