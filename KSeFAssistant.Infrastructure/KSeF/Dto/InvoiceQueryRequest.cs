using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

/// <summary>
/// Żądanie asynchronicznego zapytania o faktury zakupowe (subject2 = nabywca).
/// POST /online/Query/InvoiceQuery
/// </summary>
public sealed class InvoiceQueryRequest
{
    [JsonPropertyName("queryCriteria")]
    public required InvoiceQueryCriteria QueryCriteria { get; init; }
}

public sealed class InvoiceQueryCriteria
{
    /// <summary>"subject2" = faktury zakupowe (nabywca).</summary>
    [JsonPropertyName("subjectType")]
    public string SubjectType { get; init; } = "subject2";

    /// <summary>"incremental" = pobierz od timestampu.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "incremental";

    /// <summary>Data i czas OD (ISO 8601, UTC).</summary>
    [JsonPropertyName("acquisitionTimestampThresholdFrom")]
    public string AcquisitionTimestampFrom { get; init; } = string.Empty;

    /// <summary>Data i czas DO (ISO 8601, UTC).</summary>
    [JsonPropertyName("acquisitionTimestampThresholdTo")]
    public string AcquisitionTimestampTo { get; init; } = string.Empty;
}
