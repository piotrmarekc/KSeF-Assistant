using KSeFAssistant.Infrastructure.KSeF.Dto;
using Refit;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Refit interface mapujący endpointy KSeF REST API v2.
/// Base URL ustawiany dynamicznie przez KSeFApiClient.
/// Dokumentacja: {baseUrl}/docs/v2
/// </summary>
[Headers("Content-Type: application/json", "Accept: application/json")]
public interface IKSeFApi
{
    // =====================================================================
    //  AUTORYZACJA
    // =====================================================================

    /// <summary>
    /// Krok 1: Inicjacja uwierzytelnienia — pobierz challenge value.
    /// POST /online/Session/AuthorizationChallenge
    /// </summary>
    [Post("/online/Session/AuthorizationChallenge")]
    Task<ApiResponse<AuthorizationChallengeResponse>> GetAuthorizationChallengeAsync(
        [Body] AuthorizationChallengeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Krok 2a: Uwierzytelnienie tokenem API.
    /// POST /online/Session/InitToken
    /// </summary>
    [Post("/online/Session/InitToken")]
    Task<ApiResponse<InitSessionResponse>> InitSessionWithTokenAsync(
        [Body] InitTokenRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Krok 2b: Uwierzytelnienie certyfikatem X.509 (podpisany request).
    /// POST /online/Session/InitSigned
    /// </summary>
    [Post("/online/Session/InitSigned")]
    Task<ApiResponse<InitSessionResponse>> InitSessionWithCertificateAsync(
        [Body] string signedXmlRequest,
        CancellationToken ct = default);

    /// <summary>
    /// Zakończenie sesji.
    /// DELETE /auth/sessions/current
    /// </summary>
    [Delete("/auth/sessions/current")]
    Task<ApiResponse<string>> LogoutAsync(
        [Header("SessionToken")] string sessionToken,
        CancellationToken ct = default);

    // =====================================================================
    //  FAKTURY — QUERY (ASYNCHRONICZNE)
    // =====================================================================

    /// <summary>
    /// Krok 1: Inicjacja asynchronicznego zapytania o faktury.
    /// POST /online/Query/InvoiceQuery
    /// Zwraca referenceNumber do pollingu.
    /// </summary>
    [Post("/online/Query/InvoiceQuery")]
    Task<ApiResponse<InvoiceQueryResponse>> StartInvoiceQueryAsync(
        [Header("SessionToken")] string sessionToken,
        [Body] InvoiceQueryRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Krok 2: Sprawdź status zapytania (polling co ~2s).
    /// GET /online/Query/QueryStatus/{referenceNumber}
    /// </summary>
    [Get("/online/Query/QueryStatus/{referenceNumber}")]
    Task<ApiResponse<InvoiceQueryStatusResponse>> GetQueryStatusAsync(
        [Header("SessionToken")] string sessionToken,
        string referenceNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Krok 3: Pobierz n-tą paczkę wyników (0-indexed).
    /// GET /online/Query/QueryResult/{referenceNumber}/{partNumber}
    /// </summary>
    [Get("/online/Query/QueryResult/{referenceNumber}/{partNumber}")]
    Task<ApiResponse<InvoiceQueryResultResponse>> GetQueryResultAsync(
        [Header("SessionToken")] string sessionToken,
        string referenceNumber,
        int partNumber,
        CancellationToken ct = default);

    // =====================================================================
    //  FAKTURY — POBIERANIE XML
    // =====================================================================

    /// <summary>
    /// Pobierz surowy XML FA_v3 faktury.
    /// GET /invoices/ksef/{ksefReferenceNumber}
    /// </summary>
    [Get("/invoices/ksef/{ksefReferenceNumber}")]
    Task<ApiResponse<string>> DownloadInvoiceXmlAsync(
        [Header("SessionToken")] string sessionToken,
        string ksefReferenceNumber,
        CancellationToken ct = default);
}
