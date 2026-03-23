using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Refit;
using System.Text.Json;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Fabryka klientów KSeF API — tworzy IKSeFApi dla danego środowiska.
/// </summary>
public sealed class KSeFApiClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string? _overrideBaseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public KSeFApiClientFactory(ILoggerFactory loggerFactory, string? overrideBaseUrl = null)
    {
        _loggerFactory = loggerFactory;
        _overrideBaseUrl = overrideBaseUrl;
    }

    public KSeFApiClient Create(KSeFEnvironment environment)
    {
        var baseUrl = _overrideBaseUrl ?? environment.GetBaseUrl();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(JsonOptions)
        };

        var api = RestService.For<IKSeFApi>(httpClient, refitSettings);

        return new KSeFApiClient(api, _loggerFactory.CreateLogger<KSeFApiClient>());
    }
}
