using KSeFAssistant.Infrastructure.KSeF.Dto;
using Microsoft.Extensions.Logging;
using Refit;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Nisko-poziomowy klient KSeF API v2. Implementuje:
/// - szyfrowanie tokena RSA-OAEP SHA-256 (wymagane przez /auth/ksef-token)
/// - asynchroniczny flow uwierzytelnienia (challenge → submit → poll → redeem)
/// - paginowane pobieranie metadanych faktur
/// - retry przy HTTP 429
/// </summary>
public sealed class KSeFApiClient
{
    private readonly IKSeFApi _api;
    private readonly ILogger<KSeFApiClient> _logger;

    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
         TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(32)];

    private const int AuthPollIntervalMs = 1000;
    private const int AuthPollMaxAttempts = 60;
    private const int InvoicePageSize = 100;

    public KSeFApiClient(IKSeFApi api, ILogger<KSeFApiClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    // =====================================================================
    //  AUTORYZACJA
    // =====================================================================

    /// <summary>
    /// Pełny flow uwierzytelnienia tokenem KSeF:
    /// 1. Pobierz klucz publiczny
    /// 2. Pobierz challenge
    /// 3. Zaszyfruj {token}|{timestampMs} kluczem RSA-OAEP SHA-256
    /// 4. Wyślij zaszyfrowany token
    /// 5. Polluj status
    /// 6. Odbierz accessToken + refreshToken
    /// </summary>
    public async Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)>
        AuthenticateWithTokenAsync(string nip, string apiToken, CancellationToken ct)
    {
        // Krok 1: klucz publiczny KSeF
        var publicKey = await GetEncryptionPublicKeyAsync(ct);

        // Krok 2: challenge
        var challengeResp = await UnwrapAsync(await _api.GetAuthChallengeAsync(ct));
        _logger.LogDebug("Challenge uzyskany: {Challenge}", challengeResp.Challenge);

        // Krok 3: szyfrowanie tokena
        long timestampMs = challengeResp.TimestampMs != 0
            ? challengeResp.TimestampMs
            : challengeResp.Timestamp.ToUnixTimeMilliseconds();

        string tokenWithTimestamp = $"{apiToken}|{timestampMs}";
        byte[] plaintext = Encoding.UTF8.GetBytes(tokenWithTimestamp);
        byte[] encrypted = publicKey.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
        string encryptedB64 = Convert.ToBase64String(encrypted);

        // Krok 4: wyślij zaszyfrowany token
        var request = new AuthKsefTokenRequest
        {
            Challenge = challengeResp.Challenge,
            ContextIdentifier = new AuthContextIdentifier { Value = nip },
            EncryptedToken = encryptedB64
        };
        var sigResp = await UnwrapAsync(await _api.SubmitKsefTokenAsync(request, ct));
        string authToken = sigResp.AuthenticationToken?.Token
            ?? throw new InvalidOperationException("Brak authenticationToken w odpowiedzi.");
        string referenceNumber = sigResp.ReferenceNumber;
        string authBearer = $"Bearer {authToken}";

        _logger.LogDebug("Token przesłany, referenceNumber: {Ref}", referenceNumber);

        // Krok 5: polling statusu
        for (int attempt = 0; attempt < AuthPollMaxAttempts; attempt++)
        {
            await Task.Delay(AuthPollIntervalMs, ct);
            var statusResp = await UnwrapAsync(await _api.GetAuthStatusAsync(authBearer, referenceNumber, ct));

            if (statusResp.Status?.IsSuccess == true)
                break;

            if (statusResp.Status?.IsPending == false)
                throw new InvalidOperationException(
                    $"Uwierzytelnienie nieudane: [{statusResp.Status?.Code}] {statusResp.Status?.Description}");

            _logger.LogDebug("Auth w toku... (próba {Attempt})", attempt + 1);
        }

        // Krok 6: odbierz accessToken
        var redeemResp = await UnwrapAsync(await _api.RedeemTokenAsync(authBearer, ct));
        var access = redeemResp.AccessToken
            ?? throw new InvalidOperationException("Brak accessToken w odpowiedzi.");
        var refresh = redeemResp.RefreshToken
            ?? throw new InvalidOperationException("Brak refreshToken w odpowiedzi.");

        _logger.LogInformation("Uwierzytelnienie KSeF zakończone sukcesem, token ważny do {Until}", access.ValidUntil);
        return (access.Token, refresh.Token, access.ValidUntil);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            await _api.LogoutAsync($"Bearer {accessToken}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd wylogowywania z KSeF (ignorowany)");
        }
    }

    // =====================================================================
    //  FAKTURY
    // =====================================================================

    /// <summary>
    /// Pobiera wszystkie metadane faktur zakupowych dla podanego zakresu dat,
    /// iterując przez strony wyników.
    /// </summary>
    public async IAsyncEnumerable<InvoiceSummaryDto> QueryInvoiceMetadataAsync(
        string accessToken, DateOnly from, DateOnly to,
        IProgress<(int Done, int Total)>? progress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string bearer = $"Bearer {accessToken}";
        var request = new InvoiceMetadataRequest
        {
            DateRange = new InvoiceDateRange
            {
                DateType = "Issue",
                From = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                To = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            }
        };

        int pageOffset = 0;
        int totalYielded = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await RetryOnRateLimitAsync(async () =>
                await UnwrapAsync(await _api.QueryInvoiceMetadataAsync(
                    bearer, request, pageOffset, InvoicePageSize, ct)), ct);

            foreach (var inv in page.Invoices)
            {
                yield return inv;
                totalYielded++;
                progress?.Report((totalYielded, 0));
            }

            if (!page.HasMore || page.Invoices.Count == 0)
                break;

            pageOffset += page.Invoices.Count;
        }
    }

    public async Task<string> DownloadInvoiceXmlAsync(string accessToken, string ksefNumber, CancellationToken ct)
    {
        return await RetryOnRateLimitAsync(async () =>
            await _api.DownloadInvoiceXmlAsync($"Bearer {accessToken}", ksefNumber, ct), ct);
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private async Task<RSA> GetEncryptionPublicKeyAsync(CancellationToken ct)
    {
        var certs = await UnwrapAsync(await _api.GetPublicKeyCertificatesAsync(ct));
        var cert = certs.FirstOrDefault(c => c.IsForTokenEncryption && c.ValidTo > DateTimeOffset.UtcNow)
            ?? certs.FirstOrDefault()
            ?? throw new InvalidOperationException("Brak klucza publicznego KSeF.");

        byte[] certBytes = Convert.FromBase64String(cert.Certificate);
        using var x509 = new X509Certificate2(certBytes);
        var rsa = x509.GetRSAPublicKey()
            ?? throw new InvalidOperationException("Certyfikat KSeF nie zawiera klucza RSA.");

        // Kopiujemy RSA poza using (X509Certificate2 można zamknąć, klucz pozostaje)
        var rsaCopy = RSA.Create();
        rsaCopy.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);
        return rsaCopy;
    }

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

    private async Task<T> RetryOnRateLimitAsync<T>(Func<Task<T>> operation, CancellationToken ct)
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
        return await operation();
    }
}
