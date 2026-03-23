using KSeFAssistant.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace KSeFAssistant.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        SetWindowSize();
        NavigateToInvoices();
    }

    private void SetWindowSize()
    {
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                WinRT.Interop.WindowNative.GetWindowHandle(this)));
        appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        appWindow.Title = "KSeF Assistant";
    }

    private void NavigateToInvoices()
    {
        NavView.SelectedItem = NavInvoices;
        ContentFrame.Navigate(typeof(InvoiceListPage));
    }

    private void NavView_SelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Type? pageType = tag switch
            {
                "invoices" => typeof(InvoiceListPage),
                "settings" => typeof(AuthPage),
                "about"    => typeof(AboutPage),
                _          => null
            };
            if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
                ContentFrame.Navigate(pageType, null,
                    new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo());
        }
    }
}
