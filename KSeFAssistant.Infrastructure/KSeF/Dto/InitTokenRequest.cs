using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class InitTokenRequest
{
    [JsonPropertyName("contextIdentifier")]
    public required ContextIdentifier ContextIdentifier { get; init; }

    /// <summary>
    /// Token API zakodowany w Base64.
    /// </summary>
    [JsonPropertyName("authorisationToken")]
    public required string AuthorisationToken { get; init; }
}
