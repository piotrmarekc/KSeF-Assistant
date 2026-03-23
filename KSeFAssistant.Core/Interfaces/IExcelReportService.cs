using KSeFAssistant.Core.Models;

namespace KSeFAssistant.Core.Interfaces;

public interface IExcelReportService
{
    /// <summary>
    /// Generuje raport Excel (.xlsx) dla listy faktur.
    /// Arkusz "Faktury" + arkusz "Podsumowanie".
    /// </summary>
    Task GenerateReportAsync(IReadOnlyList<InvoiceRecord> invoices,
        FilterCriteria criteria, string outputPath, CancellationToken ct = default);
}
