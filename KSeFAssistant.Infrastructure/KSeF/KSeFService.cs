using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace KSeFAssistant.Infrastructure.KSeF;

public sealed class KSeFService : IKSeFService
{
    private readonly KSeFApiClientFactory _factory;
    private readonly KSeFDtoMapper _mapper;
    private readonly ILogger<KSeFService> _logger;

    public KSeFService(KSeFApiClientFactory factory, KSeFDtoMapper mapper,
        ILogger<KSeFService> logger)
    {
        _factory = factory;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SessionContext> AuthenticateWithTokenAsync(
        string nip, string apiToken, KSeFEnvironment environment, CancellationToken ct = default)
    {
        _logger.LogInformation("Autoryzacja tokenem KSeF dla NIP {Nip} w środowisku {Env}", nip, environment);
        var client = _factory.Create(environment);

        var (accessToken, refreshToken, expiresAt) =
            await client.AuthenticateWithTokenAsync(nip, apiToken, ct);

        return new SessionContext
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Nip = nip,
            Environment = environment,
            ExpiresAt = expiresAt.UtcDateTime
        };
    }

    public Task<SessionContext> AuthenticateWithCertificateAsync(
        string nip, string pfxPath, string pfxPassword,
        KSeFEnvironment environment, CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "Autoryzacja certyfikatem będzie dostępna w kolejnej wersji. " +
            "Do 31.12.2026 używaj autoryzacji tokenem API.");
    }

    public async IAsyncEnumerable<InvoiceRecord> GetPurchaseInvoicesAsync(
        SessionContext session, DateOnly from, DateOnly to,
        IProgress<(int Done, int Total)>? progress = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Pobieranie faktur zakupowych {From}–{To}", from, to);
        var client = _factory.Create(session.Environment);

        await foreach (var dto in client.QueryInvoiceMetadataAsync(session.AccessToken, from, to, progress, ct))
        {
            yield return _mapper.MapFromInvoiceSummary(dto);
        }
    }

    public async Task<InvoiceRecord> LoadInvoiceXmlAsync(
        SessionContext session, InvoiceRecord invoice, CancellationToken ct = default)
    {
        var client = _factory.Create(session.Environment);
        var xml = await client.DownloadInvoiceXmlAsync(session.AccessToken, invoice.KSeFNumber, ct);
        return _mapper.EnrichFromXml(invoice, xml);
    }

    public async Task LogoutAsync(SessionContext session, CancellationToken ct = default)
    {
        _logger.LogInformation("Wylogowywanie z KSeF (NIP: {Nip})", session.Nip);
        var client = _factory.Create(session.Environment);
        await client.LogoutAsync(session.AccessToken, ct);
    }
}
