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

    [JsonPropertyName("invoicingDate")]
    public DateTimeOffset InvoicingDate { get; init; }

    [JsonPropertyName("acquisitionDate")]
    public DateTimeOffset AcquisitionDate { get; init; }

    [JsonPropertyName("permanentStorageDate")]
    public DateTimeOffset PermanentStorageDate { get; init; }

    [JsonPropertyName("seller")]
    public InvoiceMetadataSellerDto? Seller { get; init; }

    [JsonPropertyName("buyer")]
    public InvoiceMetadataBuyerDto? Buyer { get; init; }

    [JsonPropertyName("netAmount")]
    public decimal NetAmount { get; init; }

    [JsonPropertyName("grossAmount")]
    public decimal GrossAmount { get; init; }

    [JsonPropertyName("vatAmount")]
    public decimal VatAmount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "PLN";

    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; init; } = string.Empty;

    [JsonPropertyName("isSelfInvoicing")]
    public bool IsSelfInvoicing { get; init; }

    [JsonPropertyName("hasAttachment")]
    public bool HasAttachment { get; init; }

    [JsonPropertyName("invoiceHash")]
    public string? InvoiceHash { get; init; }
}

/// <summary>Sprzedawca w metadanych — identyfikowany zawsze przez NIP.</summary>
public sealed class InvoiceMetadataSellerDto
{
    [JsonPropertyName("nip")]
    public string? Nip { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>Nabywca w metadanych — identyfikator może być NIP, VatUe, Other lub None.</summary>
public sealed class InvoiceMetadataBuyerDto
{
    [JsonPropertyName("identifier")]
    public InvoiceBuyerIdentifierDto? Identifier { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Wygodny dostęp do wartości identyfikatora (NIP lub inny).</summary>
    public string IdentifierValue => Identifier?.Value ?? string.Empty;
}

public sealed class InvoiceBuyerIdentifierDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;  // Nip | VatUe | Other | None

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

/// <summary>Zachowane dla wstecznej zgodności — używane w testach jednostkowych.</summary>
public sealed class InvoicePartyDto
{
    [JsonPropertyName("nip")]
    public string? Nip { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
