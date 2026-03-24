using KSeFAssistant.Core.Models;
using KSeFAssistant.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace KSeFAssistant.UI.Views;

public sealed partial class ExportDialog : ContentDialog
{
    public ExportViewModel ViewModel { get; }
    private readonly IReadOnlyList<InvoiceRecord> _invoices;

    public ExportDialog(IReadOnlyList<InvoiceRecord> invoices, SessionContext? session = null)
    {
        _invoices = invoices;
        ViewModel = App.Services.GetRequiredService<ExportViewModel>();
        ViewModel.DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        ViewModel.Session = session;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        InvoiceCountBar.Message = $"Zostaną wyeksportowane {invoices.Count} faktury.";

        this.PrimaryButtonClick += OnExportClick;
    }

    private async void OnExportClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // Zapobiegaj automatycznemu zamknięciu

        if (string.IsNullOrEmpty(ViewModel.PdfOutputFolder))
        {
            ViewModel.StatusMessage = "Wybierz folder docelowy dla PDF.";
            return;
        }

        IsPrimaryButtonEnabled = false;
        IsSecondaryButtonEnabled = false;

        await ViewModel.StartExportAsync(_invoices);

        if (ViewModel.IsComplete)
        {
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            CloseButtonText = "Zamknij";
        }
        else
        {
            IsPrimaryButtonEnabled = true;
            IsSecondaryButtonEnabled = true;
        }
    }

    private async void BrowsePdfFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        InitPickerOwner(picker);
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.Desktop;

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.PdfOutputFolder = folder.Path;
            ViewModel.ExcelOutputPath = Path.Combine(folder.Path,
                $"KSeF_raport_{DateTime.Today:yyyy-MM}.xlsx");
        }
    }

    private async void BrowseExcelPath_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitPickerOwner(picker);
        picker.DefaultFileExtension = ".xlsx";
        picker.SuggestedFileName = $"KSeF_raport_{DateTime.Today:yyyy-MM}";
        picker.FileTypeChoices.Add("Excel", [".xlsx"]);
        picker.SuggestedStartLocation = PickerLocationId.Desktop;

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
            ViewModel.ExcelOutputPath = file.Path;
    }

    private static nint GetWindowHandle()
    {
        var field = typeof(App).GetField("m_window",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var window = field?.GetValue(null) as Microsoft.UI.Xaml.Window;
        return window is not null
            ? WinRT.Interop.WindowNative.GetWindowHandle(window)
            : nint.Zero;
    }

    private static void InitPickerOwner(object picker)
    {
        var hwnd = GetWindowHandle();
        if (hwnd != nint.Zero)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
