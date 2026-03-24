using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>Odpowiedź na GET /auth/{referenceNumber}.</summary>
public sealed class AuthStatusResponse
{
    [JsonPropertyName("status")]
    public AuthOperationStatus? Status { get; init; }
}

public sealed class AuthOperationStatus
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>true gdy uwierzytelnienie zakończone sukcesem (code 200).</summary>
    public bool IsSuccess => Code == 200;

    /// <summary>true gdy operacja jeszcze w toku.</summary>
    public bool IsPending => Code is 100 or 101 or 102;
}
