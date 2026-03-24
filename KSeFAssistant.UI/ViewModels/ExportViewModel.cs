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
    private readonly IKSeFService _ksefService;
    private readonly ILogger<ExportViewModel> _logger;

    /// <summary>Aktywna sesja KSeF — wymagana do pobierania XML przed generowaniem PDF.</summary>
    public SessionContext? Session { get; set; }

    private string _pdfOutputFolder = string.Empty;
    public string PdfOutputFolder { get => _pdfOutputFolder; set => SetProperty(ref _pdfOutputFolder, value); }

    private bool _generateExcel = true;
    public bool GenerateExcel { get => _generateExcel; set => SetProperty(ref _generateExcel, value); }

    private string _excelOutputPath = string.Empty;
    public string ExcelOutputPath { get => _excelOutputPath; set => SetProperty(ref _excelOutputPath, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private int _progressValue;
    public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }

    private int _progressMax = 1;
    public int ProgressMax { get => _progressMax; set => SetProperty(ref _progressMax, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isComplete;
    public bool IsComplete { get => _isComplete; set => SetProperty(ref _isComplete, value); }

    private ExportResult? _lastResult;
    public ExportResult? LastResult { get => _lastResult; set => SetProperty(ref _lastResult, value); }

    public ExportViewModel(IPdfExportService pdfService, IExcelReportService excelService,
        IKSeFService ksefService, ILogger<ExportViewModel> logger)
    {
        _pdfService = pdfService;
        _excelService = excelService;
        _ksefService = ksefService;
        _logger = logger;

        // Domyślny folder PDF: Pulpit
        PdfOutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        ExcelOutputPath = Path.Combine(PdfOutputFolder, $"KSeF_raport_{DateTime.Today:yyyy-MM}.xlsx");
    }

    /// <summary>
    /// Called from code-behind to start export without going through the ICommand interface.
    /// </summary>
    public async Task StartExportAsync(IReadOnlyList<InvoiceRecord> invoices)
    {
        var parameters = new ExportParameters { Invoices = invoices };
        if (!CanExport(parameters)) return;
        await ExportAsync(parameters, CancellationToken.None);
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
                    // Załaduj pełny XML (pozycje, adresy) jeśli jeszcze nie załadowany
                    if (!invoice.XmlLoaded && Session is not null)
                    {
                        try { invoice = await _ksefService.LoadInvoiceXmlAsync(Session, invoice, ct); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Nie udało się pobrać XML dla {KSeFNumber}", invoice.KSeFNumber); }
                    }

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
