using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class InitSessionResponse
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("sessionToken")]
    public SessionTokenDto? SessionToken { get; init; }
}

public sealed class SessionTokenDto
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; init; } = string.Empty;

    /// <summary>Czas życia tokenu w sekundach.</summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; init; } = 3600;
}
