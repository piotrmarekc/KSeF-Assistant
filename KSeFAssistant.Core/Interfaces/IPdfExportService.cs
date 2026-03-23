using KSeFAssistant.Core.Models;

namespace KSeFAssistant.Core.Interfaces;

public interface IPdfExportService
{
    /// <summary>
    /// Generuje PDF dla jednej faktury. Zwraca bajty pliku.
    /// Nazwa pliku: {NIP}_{data}_{nrFaktury_sanitized}.pdf
    /// </summary>
    Task<byte[]> GeneratePdfAsync(InvoiceRecord invoice, CancellationToken ct = default);

    /// <summary>Zwraca sugerowaną nazwę pliku PDF dla faktury.</summary>
    string GetFileName(InvoiceRecord invoice);
}
