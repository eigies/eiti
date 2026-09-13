using System.Security.Cryptography;
using System.Text;
using eiti.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace eiti.Tests.Unit;

/// <summary>
/// La firma tiene que cerrar bit a bit con la del servicio fiscal (CallbackSigner):
/// HMAC-SHA256 sobre "{timestamp}.{rawBody}", hex minúscula, prefijo "sha256=".
/// </summary>
public class FiscalCallbackSignatureValidatorTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";
    private const string PreviousSecret = "fedcba9876543210fedcba9876543210";
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private static FiscalCallbackSignatureValidator BuildValidator(string? previous = null)
    {
        var timeProvider = new FakeTimeProvider(Now);
        var options = Options.Create(new FiscalizationOptions
        {
            BaseUrl = "https://fiscal.local",
            ApiKey = "key",
            CallbackSigningSecret = Secret,
            CallbackSigningSecretPrevious = previous
        });

        return new FiscalCallbackSignatureValidator(options, timeProvider);
    }

    // Réplica independiente del algoritmo del emisor: si alguien cambia el nuestro, esto lo caza.
    private static string Sign(string secret, byte[] body, long timestamp)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0);
        body.CopyTo(input, prefix.Length);
        return "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input)).ToLowerInvariant();
    }

    [Fact]
    public void AcceptsAValidSignature()
    {
        var body = "{\"documentId\":\"x\"}"u8.ToArray();
        var timestamp = Now.ToUnixTimeSeconds();

        BuildValidator().IsValid(body, timestamp.ToString(), Sign(Secret, body, timestamp)).Should().BeTrue();
    }

    [Fact]
    public void RejectsATamperedBody()
    {
        var body = "{\"documentId\":\"x\"}"u8.ToArray();
        var timestamp = Now.ToUnixTimeSeconds();
        var signature = Sign(Secret, body, timestamp);

        var tampered = "{\"documentId\":\"y\"}"u8.ToArray();

        BuildValidator().IsValid(tampered, timestamp.ToString(), signature).Should().BeFalse();
    }

    [Fact]
    public void RejectsAReplayOutsideTheWindow()
    {
        var body = "{}"u8.ToArray();
        var stale = Now.AddMinutes(-6).ToUnixTimeSeconds();

        BuildValidator().IsValid(body, stale.ToString(), Sign(Secret, body, stale)).Should().BeFalse();
    }

    [Fact]
    public void AcceptsATimestampInsideTheWindow()
    {
        var body = "{}"u8.ToArray();
        var recent = Now.AddMinutes(-4).ToUnixTimeSeconds();

        BuildValidator().IsValid(body, recent.ToString(), Sign(Secret, body, recent)).Should().BeTrue();
    }

    [Fact]
    public void RejectsASignatureMadeWithAnotherSecret()
    {
        var body = "{}"u8.ToArray();
        var timestamp = Now.ToUnixTimeSeconds();

        BuildValidator().IsValid(body, timestamp.ToString(), Sign("otro-secreto-de-32-bytes-exactos", body, timestamp))
            .Should().BeFalse();
    }

    // Rotación sin downtime: durante la ventana valen los dos secretos.
    [Fact]
    public void AcceptsThePreviousSecretDuringRotation()
    {
        var body = "{}"u8.ToArray();
        var timestamp = Now.ToUnixTimeSeconds();

        BuildValidator(PreviousSecret)
            .IsValid(body, timestamp.ToString(), Sign(PreviousSecret, body, timestamp))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "sha256=abcd")]
    [InlineData("not-a-number", "sha256=abcd")]
    [InlineData("1789000000", null)]
    [InlineData("1789000000", "")]
    [InlineData("1789000000", "abcd")]          // sin el prefijo sha256=
    [InlineData("1789000000", "sha256=zzzz")]   // hex inválido
    public void RejectsMalformedHeaders(string? timestamp, string? signature)
    {
        BuildValidator().IsValid("{}"u8.ToArray(), timestamp, signature).Should().BeFalse();
    }

    // Sin secreto configurado se rechaza todo: nunca "dejar pasar porque no puedo verificar".
    [Fact]
    public void RejectsEverythingWhenNoSecretIsConfigured()
    {
        var validator = new FiscalCallbackSignatureValidator(
            Options.Create(new FiscalizationOptions { BaseUrl = "https://fiscal.local", ApiKey = "key" }),
            new FakeTimeProvider(Now));

        var body = "{}"u8.ToArray();
        var timestamp = Now.ToUnixTimeSeconds();

        validator.IsValid(body, timestamp.ToString(), Sign(Secret, body, timestamp)).Should().BeFalse();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
