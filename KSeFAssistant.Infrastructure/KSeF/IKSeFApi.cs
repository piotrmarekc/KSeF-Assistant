using KSeFAssistant.Infrastructure.KSeF.Dto;
using Refit;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Refit interface mapujący endpointy KSeF REST API v2.
/// Base URL: https://api.ksef.mf.gov.pl/ — ścieżki zawierają /v2/ prefix.
/// Dokumentacja: https://api.ksef.mf.gov.pl/docs/v2/index.html
/// </summary>
[Headers("Accept: application/json")]
public interface IKSeFApi
{
    // =====================================================================
    //  KLUCZ PUBLICZNY
    // =====================================================================

    [Get("/v2/security/public-key-certificates")]
    Task<ApiResponse<IReadOnlyList<PublicKeyCertificateDto>>> GetPublicKeyCertificatesAsync(
        CancellationToken ct = default);

    // =====================================================================
    //  AUTORYZACJA — uzyskanie tokena dostępowego
    // =====================================================================

    /// POST /v2/auth/challenge — krok 1: pobierz wyzwanie
    [Post("/v2/auth/challenge")]
    Task<ApiResponse<AuthChallengeResponse>> GetAuthChallengeAsync(
        CancellationToken ct = default);

    /// POST /v2/auth/ksef-token — krok 2: prześlij zaszyfrowany token
    [Post("/v2/auth/ksef-token")]
    Task<ApiResponse<AuthSignatureResponse>> SubmitKsefTokenAsync(
        [Body] AuthKsefTokenRequest request,
        CancellationToken ct = default);

    /// GET /v2/auth/{referenceNumber} — krok 3: sprawdź status uwierzytelniania
    [Get("/v2/auth/{referenceNumber}")]
    Task<ApiResponse<AuthStatusResponse>> GetAuthStatusAsync(
        [Header("Authorization")] string authorization,
        string referenceNumber,
        CancellationToken ct = default);

    /// POST /v2/auth/token/redeem — krok 4: odbierz accessToken
    [Post("/v2/auth/token/redeem")]
    Task<ApiResponse<AuthRedeemResponse>> RedeemTokenAsync(
        [Header("Authorization")] string authorization,
        CancellationToken ct = default);

    /// POST /v2/auth/token/refresh — odśwież accessToken refreshTokenem
    [Post("/v2/auth/token/refresh")]
    Task<ApiResponse<AuthRedeemResponse>> RefreshTokenAsync(
        [Header("Authorization")] string authorization,
        CancellationToken ct = default);

    // =====================================================================
    //  SESJE
    // =====================================================================

    /// DELETE /v2/auth/sessions/current — wylogowanie
    [Delete("/v2/auth/sessions/current")]
    Task<ApiResponse<string>> LogoutAsync(
        [Header("Authorization")] string authorization,
        CancellationToken ct = default);

    // =====================================================================
    //  FAKTURY
    // =====================================================================

    /// POST /v2/invoices/query/metadata — lista metadanych faktur
    [Post("/v2/invoices/query/metadata")]
    Task<ApiResponse<InvoiceMetadataResponse>> QueryInvoiceMetadataAsync(
        [Header("Authorization")] string authorization,
        [Body] InvoiceMetadataRequest request,
        [AliasAs("pageOffset")] int pageOffset,
        [AliasAs("pageSize")] int pageSize,
        CancellationToken ct = default);

    /// GET /v2/invoices/ksef/{ksefNumber} — pobierz XML faktury (raw string — nie przez JSON deserializer)
    [Get("/v2/invoices/ksef/{ksefNumber}")]
    [Headers("Accept: application/xml, text/xml, */*")]
    Task<string> DownloadInvoiceXmlAsync(
        [Header("Authorization")] string authorization,
        string ksefNumber,
        CancellationToken ct = default);
}
