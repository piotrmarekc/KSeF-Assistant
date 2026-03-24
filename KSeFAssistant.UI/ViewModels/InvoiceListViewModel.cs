using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Core.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading;

namespace KSeFAssistant.UI.ViewModels;

public sealed partial class InvoiceListViewModel : ObservableObject
{
    private readonly IKSeFService _ksefService;
    private readonly ICredentialManager _credentials;
    private readonly InvoiceFilterService _filterService;
    private readonly IPdfExportService _pdfService;
    private readonly ILogger<InvoiceListViewModel> _logger;

    private SessionContext? _activeSession;
    private List<InvoiceItemViewModel> _allInvoices = [];

    // --- Filtry ---
    private int _selectedYear = DateTime.Today.Year;
    public int SelectedYear { get => _selectedYear; set => SetProperty(ref _selectedYear, value); }

    private int _selectedMonth = DateTime.Today.Month;
    public int SelectedMonth { get => _selectedMonth; set => SetProperty(ref _selectedMonth, value); }

    private bool _filterByNip;
    public bool FilterByNip { get => _filterByNip; set => SetProperty(ref _filterByNip, value); }

    // --- Stan ---
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private string _statusMessage = "Skonfiguruj ustawienia i pobierz faktury.";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private int _progressValue;
    public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }

    private int _progressMax = 100;
    public int ProgressMax { get => _progressMax; set => SetProperty(ref _progressMax, value); }

    private bool _isProgressVisible;
    public bool IsProgressVisible { get => _isProgressVisible; set => SetProperty(ref _isProgressVisible, value); }

    private int _selectedCount;
    public int SelectedCount { get => _selectedCount; private set => SetProperty(ref _selectedCount, value); }

    // --- Dane ---
    public ObservableCollection<InvoiceItemViewModel> DisplayedInvoices { get; } = [];
    public ObservableCollection<SupplierFilterItem> SupplierFilters { get; } = [];

    public IReadOnlyList<int> Years { get; } =
        Enumerable.Range(DateTime.Today.Year - 3, 5).Reverse().ToList();
    public IReadOnlyList<string> MonthNames { get; } =
        ["Styczeń","Luty","Marzec","Kwiecień","Maj","Czerwiec",
         "Lipiec","Sierpień","Wrzesień","Październik","Listopad","Grudzień"];

    private CancellationTokenSource? _cts;

    public InvoiceListViewModel(IKSeFService ksefService, ICredentialManager credentials,
        InvoiceFilterService filterService, IPdfExportService pdfService,
        ILogger<InvoiceListViewModel> logger)
    {
        _ksefService = ksefService;
        _credentials = credentials;
        _filterService = filterService;
        _pdfService = pdfService;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchInvoicesAsync()
    {
        if (!_credentials.HasValidCredentials())
        {
            StatusMessage = "Brak konfiguracji. Przejdź do Ustawień i zapisz dane uwierzytelniające.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 0;
        DisplayedInvoices.Clear();
        SupplierFilters.Clear();
        _allInvoices.Clear();
        StatusMessage = "Uwierzytelnianie w KSeF...";

        try
        {
            // Auth
            _activeSession = await AuthenticateAsync(_cts.Token);
            StatusMessage = $"Pobieranie faktur za {MonthNames[SelectedMonth - 1]} {SelectedYear}...";

            var from = new DateOnly(SelectedYear, SelectedMonth, 1);
            var to = from.AddMonths(1).AddDays(-1);

            int count = 0;
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                count = p.Done;
                ProgressValue = count;
                StatusMessage = $"Pobrano {count} faktur...";
            });

            await foreach (var invoice in _ksefService.GetPurchaseInvoicesAsync(
                _activeSession, from, to, progress, _cts.Token))
            {
                var vm = new InvoiceItemViewModel(invoice);
                vm.PropertyChanged += (_, _) => UpdateSelectedCount();
                _allInvoices.Add(vm);
                DisplayedInvoices.Add(vm);
            }

            // Wypełnij filtry NIP dostawców
            var suppliers = _filterService.GetUniqueSuppliers(_allInvoices.Select(x => x.Invoice).ToList());
            foreach (var (nip, name) in suppliers)
                SupplierFilters.Add(new SupplierFilterItem(nip, name));

            StatusMessage = $"Pobrano {_allInvoices.Count} faktur. Możesz teraz filtrować i eksportować.";
            _logger.LogInformation("Pobrano {Count} faktur za {Month}/{Year}",
                _allInvoices.Count, SelectedMonth, SelectedYear);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Pobieranie przerwane przez użytkownika.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania faktur");
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelFetch()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ApplyNipFilter()
    {
        var selectedNips = SupplierFilters
            .Where(s => s.IsSelected)
            .Select(s => s.Nip)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DisplayedInvoices.Clear();
        foreach (var inv in _allInvoices)
        {
            if (selectedNips.Count == 0 || selectedNips.Contains(inv.SellerNip))
                DisplayedInvoices.Add(inv);
        }
        StatusMessage = $"Wyświetlono {DisplayedInvoices.Count} z {_allInvoices.Count} faktur.";
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var inv in DisplayedInvoices)
            inv.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var inv in DisplayedInvoices)
            inv.IsSelected = false;
    }

    // --- Pobieranie PDF z KSeF ---
    private bool _isPdfDownloading;
    public bool IsPdfDownloading { get => _isPdfDownloading; set => SetProperty(ref _isPdfDownloading, value); }

    private int _pdfDownloadProgress;
    public int PdfDownloadProgress { get => _pdfDownloadProgress; set => SetProperty(ref _pdfDownloadProgress, value); }

    private int _pdfDownloadTotal;
    public int PdfDownloadTotal { get => _pdfDownloadTotal; set => SetProperty(ref _pdfDownloadTotal, value); }

    /// <summary>
    /// Pobiera XML z KSeF dla każdej faktury, generuje PDF i zapisuje do <paramref name="outputFolder"/>.
    /// Wywoływana z code-behind po wyborze folderu przez użytkownika.
    /// </summary>
    public async Task DownloadPdfsFromKSeFAsync(
        IReadOnlyList<InvoiceRecord> invoices, string outputFolder, CancellationToken ct = default)
    {
        if (_activeSession is null || invoices.Count == 0) return;

        IsPdfDownloading = true;
        IsBusy = true;
        PdfDownloadProgress = 0;
        PdfDownloadTotal = invoices.Count;
        StatusMessage = $"Pobieranie {invoices.Count} faktur z KSeF...";

        int success = 0;
        var errors = new List<string>();

        try
        {
            var semaphore = new SemaphoreSlim(3);
            var tasks = invoices.Select(async invoice =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var enriched = await _ksefService.LoadInvoiceXmlAsync(_activeSession, invoice, ct);
                    var bytes = await _pdfService.GeneratePdfAsync(enriched, ct);
                    var filePath = Path.Combine(outputFolder, _pdfService.GetFileName(enriched));
                    await File.WriteAllBytesAsync(filePath, bytes, ct);
                    Interlocked.Increment(ref success);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd PDF dla {KSeFNumber}", invoice.KSeFNumber);
                    lock (errors) errors.Add($"{invoice.InvoiceNumber}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        PdfDownloadProgress++;
                        StatusMessage = $"Pobrano {PdfDownloadProgress}/{PdfDownloadTotal} faktur...";
                    });
                }
            });

            await Task.WhenAll(tasks);

            StatusMessage = errors.Count == 0
                ? $"Pobrano {success} plików PDF do: {outputFolder}"
                : $"Pobrano {success} PDF, błędy: {errors.Count}. Szczegóły w logach.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Pobieranie przerwane.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Krytyczny błąd pobierania PDF");
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsPdfDownloading = false;
        }
    }

    public Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; set; }

    public IReadOnlyList<InvoiceRecord> GetSelectedInvoices() =>
        DisplayedInvoices
            .Where(vm => vm.IsSelected)
            .Select(vm => vm.Invoice)
            .ToList();

    public SessionContext? ActiveSession => _activeSession;

    private bool CanFetch() => !IsBusy;

    private void UpdateSelectedCount() =>
        SelectedCount = DisplayedInvoices.Count(vm => vm.IsSelected);

    private async Task<SessionContext> AuthenticateAsync(CancellationToken ct)
    {
        var nip = _credentials.LoadNip()!;
        var env = _credentials.LoadEnvironment();
        var method = _credentials.LoadAuthMethod();

        if (method == AuthMethod.Token)
        {
            var token = _credentials.LoadApiToken()!;
            return await _ksefService.AuthenticateWithTokenAsync(nip, token, env, ct);
        }
        else
        {
            var pfxPath = _credentials.LoadCertificatePath()!;
            var pfxPass = _credentials.LoadCertificatePassword()!;
            return await _ksefService.AuthenticateWithCertificateAsync(nip, pfxPath, pfxPass, env, ct);
        }
    }
}

public sealed partial class SupplierFilterItem : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public string Nip { get; }
    public string Name { get; }
    public string DisplayText => $"{Name} ({Nip})";

    public SupplierFilterItem(string nip, string name)
    {
        Nip = nip;
        Name = name;
    }
}
