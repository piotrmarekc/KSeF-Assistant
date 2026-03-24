using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>Odpowiedź na POST /auth/ksef-token lub /auth/xades-signature.</summary>
public sealed class AuthSignatureResponse
{
    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("authenticationToken")]
    public AuthOperationToken? AuthenticationToken { get; init; }
}

public sealed class AuthOperationToken
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("validUntil")]
    public DateTimeOffset ValidUntil { get; init; }
}
