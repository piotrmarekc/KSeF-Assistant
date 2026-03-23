using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Services;
using KSeFAssistant.Infrastructure.KSeF;
using KSeFAssistant.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KSeFAssistant.Infrastructure;

/// <summary>
/// Rejestracja wszystkich serwisów Infrastructure w DI container.
/// Wywołaj w App.xaml.cs: services.AddInfrastructure();
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // KSeF
        services.AddSingleton<KSeFApiClientFactory>();
        services.AddSingleton<KSeFDtoMapper>();
        services.AddSingleton<IKSeFService, KSeFService>();

        // Security
        services.AddSingleton<ICredentialManager, WindowsCredentialManager>();

        // Core services
        services.AddSingleton<InvoiceFilterService>();
        services.AddSingleton<IPdfExportService, PdfExportService>();
        services.AddSingleton<IExcelReportService, ExcelReportService>();

        return services;
    }
}
