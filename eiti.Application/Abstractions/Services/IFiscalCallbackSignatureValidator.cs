namespace eiti.Application.Abstractions.Services;

/// <summary>
/// Valida la firma HMAC con la que el servicio fiscal firma sus callbacks.
/// Se firma el body CRUDO (los mismos bytes que llegaron), nunca uno re-serializado.
/// </summary>
public interface IFiscalCallbackSignatureValidator
{
    bool IsValid(ReadOnlySpan<byte> rawBody, string? timestampHeader, string? signatureHeader);
}
