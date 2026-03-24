using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class InvoiceMetadataRequest
{
    [JsonPropertyName("subjectType")]
    public string SubjectType { get; init; } = "Subject2"; // Subject2 = nabywca (zakup)

    [JsonPropertyName("dateRange")]
    public required InvoiceDateRange DateRange { get; init; }
}

public sealed class InvoiceDateRange
{
    [JsonPropertyName("dateType")]
    public string DateType { get; init; } = "Issue";

    [JsonPropertyName("from")]
    public DateTimeOffset From { get; init; }

    [JsonPropertyName("to")]
    public DateTimeOffset? To { get; init; }
}
