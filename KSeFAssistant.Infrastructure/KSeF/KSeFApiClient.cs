using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF.Dto;
using Microsoft.Extensions.Logging;
using Refit;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Nisko-poziomowy klient KSeF. Opakowuje IKSeFApi w obsługę błędów i retry.
/// Używany przez KSeFService.
/// </summary>
public sealed class KSeFApiClient
{
    private readonly IKSeFApi _api;
    private readonly ILogger<KSeFApiClient> _logger;

    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
         TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(32)];

    public KSeFApiClient(IKSeFApi api, ILogger<KSeFApiClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    // =====================================================================
    //  AUTORYZACJA
    // =====================================================================

    public async Task<AuthorizationChallengeResponse> GetChallengeAsync(
        string nip, CancellationToken ct)
    {
        var response = await _api.GetAuthorizationChallengeAsync(
            new AuthorizationChallengeRequest
            {
                ContextIdentifier = new ContextIdentifier { Identifier = nip }
            }, ct);

        return await UnwrapAsync(response);
    }

    public async Task<InitSessionResponse> InitTokenSessionAsync(
        string nip, string apiToken, CancellationToken ct)
    {
        var tokenBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiToken));
        var response = await _api.InitSessionWithTokenAsync(
            new InitTokenRequest
            {
                ContextIdentifier = new ContextIdentifier { Identifier = nip },
                AuthorisationToken = tokenBase64
            }, ct);

        return await UnwrapAsync(response);
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken ct)
    {
        try
        {
            await _api.LogoutAsync(sessionToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd podczas wylogowywania z KSeF (ignorowany)");
        }
    }

    // =====================================================================
    //  ZAPYTANIA O FAKTURY
    // =====================================================================

    public async Task<InvoiceQueryResponse> StartQueryAsync(
        string sessionToken, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var request = new InvoiceQueryRequest
        {
            QueryCriteria = new InvoiceQueryCriteria
            {
                AcquisitionTimestampFrom = $"{from:yyyy-MM-dd}T00:00:00",
                AcquisitionTimestampTo   = $"{to:yyyy-MM-dd}T23:59:59"
            }
        };
        var response = await _api.StartInvoiceQueryAsync(sessionToken, request, ct);
        return await UnwrapAsync(response);
    }

    public async Task<InvoiceQueryStatusResponse> PollQueryStatusAsync(
        string sessionToken, string referenceNumber, CancellationToken ct)
    {
        return await RetryOnRateLimitAsync(async () =>
        {
            var response = await _api.GetQueryStatusAsync(sessionToken, referenceNumber, ct);
            return await UnwrapAsync(response);
        }, ct);
    }

    public async Task<InvoiceQueryResultResponse> GetQueryResultPartAsync(
        string sessionToken, string referenceNumber, int partNumber, CancellationToken ct)
    {
        return await RetryOnRateLimitAsync(async () =>
        {
            var response = await _api.GetQueryResultAsync(sessionToken, referenceNumber, partNumber, ct);
            return await UnwrapAsync(response);
        }, ct);
    }

    public async Task<string> DownloadInvoiceXmlAsync(
        string sessionToken, string ksefNumber, CancellationToken ct)
    {
        return await RetryOnRateLimitAsync(async () =>
        {
            var response = await _api.DownloadInvoiceXmlAsync(sessionToken, ksefNumber, ct);
            return await UnwrapAsync(response);
        }, ct);
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private async Task<T> UnwrapAsync<T>(ApiResponse<T> response)
    {
        if (response.IsSuccessStatusCode && response.Content is not null)
            return response.Content;

        KSeFErrorResponse? error = null;
        if (response.Error?.Content is { } errorContent)
        {
            try { error = JsonSerializer.Deserialize<KSeFErrorResponse>(errorContent); }
            catch { /* ignoruj błąd parsowania błędu */ }
        }

        _logger.LogError("KSeF API error {StatusCode}: {Error}",
            response.StatusCode, error?.Exception?.ExceptionDescription ?? response.Error?.Message);

        throw new KSeFApiException(
            response.StatusCode,
            response.Error?.Message ?? "Nieznany błąd",
            error);
    }

    private async Task<T> RetryOnRateLimitAsync<T>(
        Func<Task<T>> operation, CancellationToken ct)
    {
        for (int attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (KSeFApiException ex) when (ex.IsRateLimited)
            {
                var delay = RetryDelays[attempt];
                _logger.LogWarning("KSeF rate limit — czekam {Delay}s (próba {Attempt}/{Max})",
                    delay.TotalSeconds, attempt + 1, RetryDelays.Length);

                await Task.Delay(delay, ct);
            }
        }
        return await operation(); // ostatnia próba — rzuć jeśli nie przejdzie
    }
}
