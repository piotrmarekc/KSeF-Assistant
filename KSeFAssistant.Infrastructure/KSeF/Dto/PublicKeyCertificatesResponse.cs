using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class PublicKeyCertificatesResponse
{
    [JsonPropertyName("certificates")]
    public IReadOnlyList<PublicKeyCertificateDto> Certificates { get; init; } = [];
}

public sealed class PublicKeyCertificateDto
{
    /// <summary>Certyfikat zakodowany w Base64 (DER).</summary>
    [JsonPropertyName("certificate")]
    public string Certificate { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset ValidTo { get; init; }

    /// <summary>Np. ["KsefTokenEncryption", "SymmetricKeyEncryption"].</summary>
    [JsonPropertyName("usage")]
    public IReadOnlyList<string> Usage { get; init; } = [];

    public bool IsForTokenEncryption =>
        Usage.Any(u => string.Equals(u, "KsefTokenEncryption", StringComparison.OrdinalIgnoreCase));
}
