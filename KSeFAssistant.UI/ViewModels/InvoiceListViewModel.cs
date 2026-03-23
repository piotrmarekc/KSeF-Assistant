using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Core.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace KSeFAssistant.UI.ViewModels;

public sealed partial class InvoiceListViewModel : ObservableObject
{
    private readonly IKSeFService _ksefService;
    private readonly ICredentialManager _credentials;
    private readonly InvoiceFilterService _filterService;
    private readonly ILogger<InvoiceListViewModel> _logger;

    private SessionContext? _activeSession;
    private List<InvoiceItemViewModel> _allInvoices = [];

    // --- Filtry ---
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
    [ObservableProperty] private bool _filterByNip;

    // --- Stan ---
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Skonfiguruj ustawienia i pobierz faktury.";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 100;
    [ObservableProperty] private bool _isProgressVisible;
    [ObservableProperty] private int _selectedCount;

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
        InvoiceFilterService filterService, ILogger<InvoiceListViewModel> logger)
    {
        _ksefService = ksefService;
        _credentials = credentials;
        _filterService = filterService;
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
    [ObservableProperty] private bool _isSelected;
    public string Nip { get; }
    public string Name { get; }
    public string DisplayText => $"{Name} ({Nip})";

    public SupplierFilterItem(string nip, string name)
    {
        Nip = nip;
        Name = name;
    }
}
