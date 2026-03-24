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

        _logger.LogInformation("Pobrano XML dla {KSeFNumber}: {Len} znaków, pierwsze 200: {Preview}",
            invoice.KSeFNumber, xml?.Length ?? 0,
            xml?.Length > 200 ? xml[..200] : xml);

        var enriched = _mapper.EnrichFromXml(invoice, xml!);

        if (enriched.LineItems.Count == 0 || enriched.ParseError != null)
        {
            // Zapisz XML do %TEMP% dla diagnozy
            var safeNum = string.Join("_", invoice.KSeFNumber.Split(Path.GetInvalidFileNameChars()));
            var debugPath = Path.Combine(Path.GetTempPath(), $"ksef_debug_{safeNum}.xml");
            await File.WriteAllTextAsync(debugPath, xml ?? "(null)", ct);
            _logger.LogWarning(
                "Faktura {KSeFNumber}: pozycji={Items}, ParseError={Err}. XML zapisany: {Path}",
                invoice.KSeFNumber, enriched.LineItems.Count, enriched.ParseError, debugPath);
        }

        return enriched;
    }

    public async Task LogoutAsync(SessionContext session, CancellationToken ct = default)
    {
        _logger.LogInformation("Wylogowywanie z KSeF (NIP: {Nip})", session.Nip);
        var client = _factory.Create(session.Environment);
        await client.LogoutAsync(session.AccessToken, ct);
    }
}
