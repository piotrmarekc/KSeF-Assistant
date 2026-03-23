using KSeFAssistant.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using System.IO;

namespace KSeFAssistant.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Przechowujemy referencję statycznie, aby ExportDialog/AuthPage mogły uzyskać HWND
    internal static Window? m_window;

    public App()
    {
        ConfigureLogging();
        Services = ConfigureServices();
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }

    private static void ConfigureLogging()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KSeFAssistant", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(dispose: true);
        });

        // Infrastructure (KSeF, PDF, Excel, CredentialManager)
        services.AddInfrastructure();

        // ViewModels
        services.AddTransient<ViewModels.AuthViewModel>();
        services.AddTransient<ViewModels.InvoiceListViewModel>();
        services.AddTransient<ViewModels.ExportViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Nieobsłużony wyjątek aplikacji");
        e.Handled = true;
        ShowFatalErrorDialogAsync(e.Exception);
    }

    private static async void ShowFatalErrorDialogAsync(Exception ex)
    {
        try
        {
            if (m_window?.Content?.XamlRoot is { } root)
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Krytyczny błąd",
                    Content = $"Wystąpił nieoczekiwany błąd:\n\n{ex.Message}\n\nSprawdź logi w %LOCALAPPDATA%\\KSeFAssistant\\logs\\",
                    CloseButtonText = "Zamknij",
                    XamlRoot = root
                };
                await dialog.ShowAsync();
            }
        }
        finally
        {
            Log.CloseAndFlush();
            Environment.Exit(1);
        }
    }
}

