using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>Odpowiedź na POST /auth/token/redeem lub /auth/token/refresh.</summary>
public sealed class AuthRedeemResponse
{
    [JsonPropertyName("accessToken")]
    public AuthTokenInfo? AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public AuthTokenInfo? RefreshToken { get; init; }
}

public sealed class AuthTokenInfo
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("validUntil")]
    public DateTimeOffset ValidUntil { get; init; }
}
