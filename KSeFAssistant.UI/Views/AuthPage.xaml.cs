using KSeFAssistant.Core.Models;
using KSeFAssistant.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace KSeFAssistant.UI.Views;

public sealed partial class AuthPage : Page
{
    public AuthViewModel ViewModel { get; }

    public AuthPage()
    {
        ViewModel = App.Services.GetRequiredService<AuthViewModel>();
        this.DataContext = ViewModel;
        this.InitializeComponent();
        EnvironmentCombo.ItemsSource = ViewModel.Environments;
        EnvironmentCombo.SelectedItem = ViewModel.SelectedEnvironment;
        AuthMethodCombo.ItemsSource = ViewModel.AuthMethods;
        AuthMethodCombo.SelectedItem = ViewModel.SelectedAuthMethod;
    }

    private void EnvironmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is KSeFEnvironment env)
            ViewModel.SelectedEnvironment = env;
    }

    private void AuthMethodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is AuthMethod method)
            ViewModel.SelectedAuthMethod = method;
    }

    private async void BrowseCertificate_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();

        // Ustawienie właściciela okna (wymagane w WinUI 3)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            (Microsoft.UI.Xaml.Application.Current as App)!
            .GetType().GetField("m_window",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".pfx");
        picker.FileTypeFilter.Add(".p12");
        picker.FileTypeFilter.Add(".crt");
        picker.FileTypeFilter.Add(".cer");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            ViewModel.CertificatePath = file.Path;
    }
}
