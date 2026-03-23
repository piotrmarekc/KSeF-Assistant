using System.Text.Json.Serialization;

namespace KSeFAssistant.Infrastructure.KSeF.Dto;

public sealed class KSeFErrorResponse
{
    [JsonPropertyName("exception")]
    public KSeFException? Exception { get; init; }

    [JsonPropertyName("serviceCtx")]
    public string? ServiceCtx { get; init; }

    [JsonPropertyName("serviceCode")]
    public string? ServiceCode { get; init; }

    [JsonPropertyName("serviceErrorCode")]
    public string? ServiceErrorCode { get; init; }
}

public sealed class KSeFException
{
    [JsonPropertyName("exceptionCode")]
    public int ExceptionCode { get; init; }

    [JsonPropertyName("exceptionDescription")]
    public string ExceptionDescription { get; init; } = string.Empty;

    [JsonPropertyName("exceptionDetailList")]
    public IReadOnlyList<KSeFExceptionDetail> ExceptionDetailList { get; init; } = [];
}

public sealed class KSeFExceptionDetail
{
    [JsonPropertyName("exceptionCode")]
    public int ExceptionCode { get; init; }

    [JsonPropertyName("exceptionDescription")]
    public string ExceptionDescription { get; init; } = string.Empty;
}
