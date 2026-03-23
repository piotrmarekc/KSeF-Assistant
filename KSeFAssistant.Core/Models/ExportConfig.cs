namespace KSeFAssistant.Core.Models;

public sealed class ExportConfig
{
    /// <summary>Folder docelowy dla plików PDF.</summary>
    public required string PdfOutputFolder { get; init; }

    /// <summary>Generuj raport Excel.</summary>
    public bool GenerateExcel { get; init; }

    /// <summary>Pełna ścieżka do pliku Excel (gdy GenerateExcel = true).</summary>
    public string? ExcelOutputPath { get; init; }

    /// <summary>Liczba równoległych wątków przy generowaniu PDF.</summary>
    public int PdfDegreeOfParallelism { get; init; } = 4;
}

public sealed class ExportResult
{
    public int PdfSuccessCount { get; init; }
    public int PdfErrorCount { get; init; }
    public string? ExcelPath { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
