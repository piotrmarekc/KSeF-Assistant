using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>
/// Odpowiedź na inicjację zapytania (POST /online/Query/InvoiceQuery).
/// Zawiera referenceNumber do pollingu statusu.
/// </summary>
public sealed class InvoiceQueryResponse
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("processingCode")]
    public int ProcessingCode { get; init; }
}
