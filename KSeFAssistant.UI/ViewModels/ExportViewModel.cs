using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace KSeFAssistant.UI.ViewModels;

public sealed partial class ExportViewModel : ObservableObject
{
    private readonly IPdfExportService _pdfService;
    private readonly IExcelReportService _excelService;
    private readonly ILogger<ExportViewModel> _logger;

    [ObservableProperty] private string _pdfOutputFolder = string.Empty;
    [ObservableProperty] private bool _generateExcel = true;
    [ObservableProperty] private string _excelOutputPath = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private ExportResult? _lastResult;

    public ExportViewModel(IPdfExportService pdfService, IExcelReportService excelService,
        ILogger<ExportViewModel> logger)
    {
        _pdfService = pdfService;
        _excelService = excelService;
        _logger = logger;

        // Domyślny folder PDF: Pulpit
        PdfOutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        ExcelOutputPath = Path.Combine(PdfOutputFolder, $"KSeF_raport_{DateTime.Today:yyyy-MM}.xlsx");
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(ExportParameters parameters, CancellationToken ct)
    {
        IsBusy = true;
        IsComplete = false;
        ProgressValue = 0;
        ProgressMax = parameters.Invoices.Count;
        StatusMessage = $"Generowanie {parameters.Invoices.Count} plików PDF...";

        var errors = new List<string>();
        int success = 0;

        try
        {
            // Generuj PDF równolegle (max 4 wątki)
            var semaphore = new SemaphoreSlim(4);
            var tasks = parameters.Invoices.Select(async invoice =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var bytes = await _pdfService.GeneratePdfAsync(invoice, ct);
                    var fileName = _pdfService.GetFileName(invoice);
                    var filePath = Path.Combine(PdfOutputFolder, fileName);
                    await File.WriteAllBytesAsync(filePath, bytes, ct);

                    Interlocked.Increment(ref success);
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        ProgressValue++;
                        StatusMessage = $"PDF: {ProgressValue}/{ProgressMax}";
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd generowania PDF dla {KSeFNumber}", invoice.KSeFNumber);
                    lock (errors) errors.Add($"{invoice.InvoiceNumber}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Excel
            string? excelPath = null;
            if (GenerateExcel && parameters.Invoices.Count > 0)
            {
                StatusMessage = "Generowanie raportu Excel...";
                var config = new FilterCriteria
                {
                    PeriodStart = parameters.Invoices.Min(x => x.IssueDate),
                    PeriodEnd = parameters.Invoices.Max(x => x.IssueDate)
                };
                await _excelService.GenerateReportAsync(parameters.Invoices, config, ExcelOutputPath, ct);
                excelPath = ExcelOutputPath;
            }

            LastResult = new ExportResult
            {
                PdfSuccessCount = success,
                PdfErrorCount = errors.Count,
                ExcelPath = excelPath,
                Errors = errors
            };

            StatusMessage = $"Gotowe! PDF: {success} (błędy: {errors.Count})" +
                            (excelPath is not null ? $" | Excel: {Path.GetFileName(excelPath)}" : "");
            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Eksport przerwany.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Krytyczny błąd eksportu");
            StatusMessage = $"Błąd krytyczny: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExport(ExportParameters? p) =>
        !IsBusy && !string.IsNullOrEmpty(PdfOutputFolder) && p?.Invoices.Count > 0;

    // Referencja do DispatcherQueue (ustawiana przez View)
    public Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; set; }
}

public sealed class ExportParameters
{
    public required IReadOnlyList<InvoiceRecord> Invoices { get; init; }
}
