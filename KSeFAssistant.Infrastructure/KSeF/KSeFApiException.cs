using KSeFAssistant.Infrastructure.KSeF.Dto;
using System.Net;

namespace KSeFAssistant.Infrastructure.KSeF;

public sealed class KSeFApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public int? KSeFExceptionCode { get; }

    public KSeFApiException(HttpStatusCode statusCode, string message,
        KSeFErrorResponse? error = null, Exception? inner = null)
        : base(BuildMessage(statusCode, message, error), inner)
    {
        StatusCode = statusCode;
        KSeFExceptionCode = error?.Exception?.ExceptionCode;
    }

    private static string BuildMessage(HttpStatusCode code, string msg, KSeFErrorResponse? err)
    {
        if (err?.Exception is { } ex)
            return $"KSeF {(int)code}: [{ex.ExceptionCode}] {ex.ExceptionDescription}";
        return $"KSeF {(int)code}: {msg}";
    }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
    public bool IsServerError => (int)StatusCode >= 500;
}
