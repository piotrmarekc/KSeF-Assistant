using KSeFAssistant.Core.Models;

namespace KSeFAssistant.Core.Interfaces;

public interface IKSeFService
{
    /// <summary>Autoryzacja tokenem API (ważna do 31.12.2026).</summary>
    Task<SessionContext> AuthenticateWithTokenAsync(string nip, string apiToken,
        KSeFEnvironment environment, CancellationToken ct = default);

    /// <summary>Autoryzacja certyfikatem X.509 (.pfx) — wymagana od 01.01.2027.</summary>
    Task<SessionContext> AuthenticateWithCertificateAsync(string nip, string pfxPath, string pfxPassword,
        KSeFEnvironment environment, CancellationToken ct = default);

    /// <summary>
    /// Pobiera listę faktur zakupowych (subject2) za podany okres.
    /// Wyniki streamowane przez IAsyncEnumerable.
    /// </summary>
    IAsyncEnumerable<InvoiceRecord> GetPurchaseInvoicesAsync(SessionContext session,
        DateOnly from, DateOnly to, IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default);

    /// <summary>Pobiera surowy XML FA_v3 faktury i parsuje do InvoiceRecord (z pozycjami).</summary>
    Task<InvoiceRecord> LoadInvoiceXmlAsync(SessionContext session, InvoiceRecord invoice,
        CancellationToken ct = default);

    /// <summary>Kończy sesję KSeF (DELETE /auth/sessions/current).</summary>
    Task LogoutAsync(SessionContext session, CancellationToken ct = default);
}
