using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class InvoiceMetadataResponse
{
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }

    [JsonPropertyName("isTruncated")]
    public bool IsTruncated { get; init; }

    [JsonPropertyName("invoices")]
    public IReadOnlyList<InvoiceSummaryDto> Invoices { get; init; } = [];
}

public sealed class InvoiceSummaryDto
{
    [JsonPropertyName("ksefNumber")]
    public string KsefNumber { get; init; } = string.Empty;

    [JsonPropertyName("invoiceNumber")]
    public string InvoiceNumber { get; init; } = string.Empty;

    [JsonPropertyName("issueDate")]
    public DateTimeOffset IssueDate { get; init; }

    [JsonPropertyName("acquisitionDate")]
    public DateTimeOffset AcquisitionDate { get; init; }

    [JsonPropertyName("seller")]
    public InvoicePartyDto? Seller { get; init; }

    [JsonPropertyName("buyer")]
    public InvoicePartyDto? Buyer { get; init; }

    [JsonPropertyName("netAmount")]
    public decimal NetAmount { get; init; }

    [JsonPropertyName("grossAmount")]
    public decimal GrossAmount { get; init; }

    [JsonPropertyName("vatAmount")]
    public decimal VatAmount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "PLN";
}

public sealed class InvoicePartyDto
{
    [JsonPropertyName("nip")]
    public string? Nip { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
