using KSeFAssistant.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace KSeFAssistant.UI.Views;

public sealed partial class InvoiceListPage : Page
{
    public InvoiceListViewModel ViewModel { get; }

    private CancellationTokenSource? _pdfDownloadCts;

    public InvoiceListPage()
    {
        ViewModel = App.Services.GetRequiredService<InvoiceListViewModel>();
        ViewModel.DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        this.DataContext = ViewModel;
        this.InitializeComponent();
        YearCombo.ItemsSource = ViewModel.Years;
        YearCombo.SelectedIndex = ViewModel.Years.ToList().IndexOf(ViewModel.SelectedYear);
        MonthCombo.ItemsSource = ViewModel.MonthNames;
        MonthCombo.SelectedIndex = ViewModel.SelectedMonth - 1;

        // Update invoice count label when collection changes
        ViewModel.DisplayedInvoices.CollectionChanged += (_, _) =>
            InvoiceCountBlock.Text = ViewModel.DisplayedInvoices.Count.ToString();
        InvoiceCountBlock.Text = ViewModel.DisplayedInvoices.Count.ToString();
    }

    private void YearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is int year)
            ViewModel.SelectedYear = year;
    }

    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedMonth = MonthCombo.SelectedIndex + 1;
    }

    private async void ExportSelected_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.GetSelectedInvoices();
        if (selected.Count == 0)
        {
            var dialog = new ContentDialog
            {
                Title = "Brak zaznaczonych faktur",
                Content = "Zaznacz co najmniej jedną fakturę przed eksportem.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        var exportDialog = new ExportDialog(selected) { XamlRoot = XamlRoot };
        await exportDialog.ShowAsync();
    }

    private async void DownloadPdfFromKSeF_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.GetSelectedInvoices();
        if (selected.Count == 0)
        {
            var dialog = new ContentDialog
            {
                Title = "Brak zaznaczonych faktur",
                Content = "Zaznacz co najmniej jedną fakturę przed pobieraniem.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        if (ViewModel.ActiveSession is null)
        {
            var dialog = new ContentDialog
            {
                Title = "Brak aktywnej sesji",
                Content = "Pobierz faktury z KSeF przed pobraniem plików PDF.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        var picker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        _pdfDownloadCts = new CancellationTokenSource();
        await ViewModel.DownloadPdfsFromKSeFAsync(selected, folder.Path, _pdfDownloadCts.Token);
        _pdfDownloadCts = null;
    }

    private void CancelPdfDownload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _pdfDownloadCts?.Cancel();
    }
}
