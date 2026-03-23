using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class AuthorizationChallengeRequest
{
    [JsonPropertyName("contextIdentifier")]
    public required ContextIdentifier ContextIdentifier { get; init; }
}

public sealed class ContextIdentifier
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "onip";   // "onip" = NIP organizacji

    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }  // NIP (10 cyfr)
}
