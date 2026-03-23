using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>
/// Paczka wyników zapytania (GET /online/Query/QueryResult?QueryPartNumber={n}).
/// </summary>
public sealed class InvoiceQueryResultResponse
{
    [JsonPropertyName("referenceNumber")]
    public string ReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("partReferenceNumber")]
    public string PartReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("invoiceHeaderList")]
    public IReadOnlyList<InvoiceHeaderDto> InvoiceHeaderList { get; init; } = [];
}

/// <summary>
/// Metadane pojedynczej faktury z listy wyników (bez szczegółów/XML).
/// </summary>
public sealed class InvoiceHeaderDto
{
    [JsonPropertyName("ksefReferenceNumber")]
    public string KSeFReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("invoiceReferenceNumber")]
    public string InvoiceReferenceNumber { get; init; } = string.Empty;

    [JsonPropertyName("acquisitionTimestamp")]
    public string AcquisitionTimestamp { get; init; } = string.Empty;

    [JsonPropertyName("subjectBy")]
    public InvoiceSubjectDto? SubjectBy { get; init; }    // Sprzedawca

    [JsonPropertyName("subjectTo")]
    public InvoiceSubjectDto? SubjectTo { get; init; }    // Nabywca

    [JsonPropertyName("invoicingDate")]
    public string InvoicingDate { get; init; } = string.Empty;

    [JsonPropertyName("net")]
    public decimal Net { get; init; }

    [JsonPropertyName("vat")]
    public decimal Vat { get; init; }

    [JsonPropertyName("gross")]
    public decimal Gross { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "PLN";

    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; init; } = string.Empty;
}

public sealed class InvoiceSubjectDto
{
    [JsonPropertyName("issuedByIdentifier")]
    public SubjectIdentifierDto? Identifier { get; init; }

    [JsonPropertyName("issuedByName")]
    public SubjectNameDto? Name { get; init; }
}

public sealed class SubjectIdentifierDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("identifier")]
    public string Identifier { get; init; } = string.Empty;
}

public sealed class SubjectNameDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("tradeName")]
    public string TradeName { get; init; } = string.Empty;
}
