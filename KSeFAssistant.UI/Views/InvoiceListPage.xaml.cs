using KSeFAssistant.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace KSeFAssistant.UI.Views;

public sealed partial class InvoiceListPage : Page
{
    public InvoiceListViewModel ViewModel { get; }

    public InvoiceListPage()
    {
        ViewModel = App.Services.GetRequiredService<InvoiceListViewModel>();
        this.InitializeComponent();
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
}
