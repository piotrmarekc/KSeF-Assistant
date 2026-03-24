using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class AuthKsefTokenRequest
{
    [JsonPropertyName("challenge")]
    public required string Challenge { get; init; }

    [JsonPropertyName("contextIdentifier")]
    public required AuthContextIdentifier ContextIdentifier { get; init; }

    [JsonPropertyName("encryptedToken")]
    public required string EncryptedToken { get; init; }

    [JsonPropertyName("authorizationPolicy")]
    public object? AuthorizationPolicy { get; init; } = null;
}

public sealed class AuthContextIdentifier
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "nip";

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
