using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>
/// Odpowiedź na polling statusu zapytania (GET /online/Query/QueryStatus).
/// </summary>
public sealed class InvoiceQueryStatusResponse
{
    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("processingCode")]
    public int ProcessingCode { get; init; }

    [JsonPropertyName("processingDescription")]
    public string ProcessingDescription { get; init; } = string.Empty;

    /// <summary>Liczba paczek (parcel) z wynikami. Dostępne gdy processingCode = 200.</summary>
    [JsonPropertyName("numberOfParts")]
    public int NumberOfParts { get; init; }

    public bool IsCompleted => ProcessingCode == 200;
    public bool IsProcessing => ProcessingCode is 100 or 102;
    public bool IsFailed => ProcessingCode >= 400;
}
