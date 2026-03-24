using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class AuthChallengeResponse
{
    [JsonPropertyName("challenge")]
    public string Challenge { get; init; } = string.Empty;

    /// <summary>ISO 8601 — czas wygenerowania wyzwania.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Unix timestamp w ms — gotowy do użycia w szyfrze.</summary>
    [JsonPropertyName("timestampMs")]
    public long TimestampMs { get; init; }
}
