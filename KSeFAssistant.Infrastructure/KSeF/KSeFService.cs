using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF.Dto;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Główny serwis KSeF: implementuje IKSeFService.
/// Orkiestruje auth flow, polling i pobieranie faktur.
/// </summary>
public sealed class KSeFService : IKSeFService
{
    private readonly KSeFApiClientFactory _factory;
    private readonly KSeFDtoMapper _mapper;
    private readonly ILogger<KSeFService> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private const int MaxPollingAttempts = 120; // 4 minuty

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
        _logger.LogInformation("Autoryzacja tokenem dla NIP {Nip} w środowisku {Env}", nip, environment);

        var client = _factory.Create(environment);

        // Krok 1: challenge
        var challenge = await client.GetChallengeAsync(nip, ct);
        _logger.LogDebug("Challenge uzyskany: {Challenge}", challenge.Challenge);

        // Krok 2: init session z tokenem
        var session = await client.InitTokenSessionAsync(nip, apiToken, ct);

        if (session.SessionToken is null)
            throw new InvalidOperationException("KSeF nie zwrócił tokenu sesji.");

        _logger.LogInformation("Sesja KSeF aktywna, referencja: {Ref}", session.ReferenceNumber);

        return new SessionContext
        {
            SessionToken = session.SessionToken.Token,
            Nip = nip,
            Environment = environment,
            ReferenceNumber = session.ReferenceNumber,
            ExpiresAt = DateTime.UtcNow.AddSeconds(session.SessionToken.Ttl)
        };
    }

    public async Task<SessionContext> AuthenticateWithCertificateAsync(
        string nip, string pfxPath, string pfxPassword, KSeFEnvironment environment,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Autoryzacja certyfikatem dla NIP {Nip}", nip);

        // TODO: Pełna implementacja wymaga podpisania requestu certyfikatem X.509.
        // W KSeF 2.0 inicjacja przez POST /online/Session/InitSigned z podpisanym XML.
        // Implementacja z System.Security.Cryptography.X509Certificates.
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
        var token = session.SessionToken;

        // Krok 1: Inicjacja zapytania
        var queryResponse = await client.StartQueryAsync(token, from, to, ct);
        var refNumber = queryResponse.ReferenceNumber;
        _logger.LogDebug("Zapytanie inicjowane, ref: {Ref}", refNumber);

        // Krok 2: Polling statusu
        InvoiceQueryStatusResponse? status = null;
        for (int attempt = 0; attempt < MaxPollingAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PollingInterval, ct);

            status = await client.PollQueryStatusAsync(token, refNumber, ct);

            if (status.IsCompleted) break;
            if (status.IsFailed)
                throw new InvalidOperationException(
                    $"Zapytanie KSeF nieudane: {status.ProcessingDescription}");

            _logger.LogDebug("Polling {Attempt}/{Max}: {Code}", attempt + 1,
                MaxPollingAttempts, status.ProcessingCode);
        }

        if (status is null || !status.IsCompleted)
            throw new TimeoutException("Przekroczono czas oczekiwania na wyniki zapytania KSeF.");

        int totalParts = status.NumberOfParts;
        _logger.LogInformation("Zapytanie ukończone, {Parts} paczek wyników", totalParts);

        // Krok 3: Pobieranie paczek wyników
        int totalInvoices = 0;
        for (int part = 0; part < totalParts; part++)
        {
            ct.ThrowIfCancellationRequested();

            var result = await client.GetQueryResultPartAsync(token, refNumber, part, ct);

            foreach (var header in result.InvoiceHeaderList)
            {
                var invoice = _mapper.MapFromHeader(header);
                totalInvoices++;
                progress?.Report((totalInvoices, -1)); // Total nieznany z góry
                yield return invoice;
            }

            _logger.LogDebug("Paczka {Part}/{Total}: {Count} faktur",
                part + 1, totalParts, result.InvoiceHeaderList.Count);
        }

        _logger.LogInformation("Pobrano łącznie {Total} faktur zakupowych", totalInvoices);
    }

    public async Task<InvoiceRecord> LoadInvoiceXmlAsync(
        SessionContext session, InvoiceRecord invoice, CancellationToken ct = default)
    {
        _logger.LogDebug("Pobieranie XML dla faktury {KSeFNumber}", invoice.KSeFNumber);

        var client = _factory.Create(session.Environment);
        var xml = await client.DownloadInvoiceXmlAsync(session.SessionToken, invoice.KSeFNumber, ct);
        return _mapper.EnrichFromXml(invoice, xml);
    }

    public async Task LogoutAsync(SessionContext session, CancellationToken ct = default)
    {
        _logger.LogInformation("Wylogowywanie z KSeF (NIP: {Nip})", session.Nip);
        var client = _factory.Create(session.Environment);
        await client.LogoutAsync(session.SessionToken, ct);
    }
}
