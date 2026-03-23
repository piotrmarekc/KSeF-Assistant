using FluentAssertions;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace KSeFAssistant.Tests.Integration;

/// <summary>
/// Testy integracyjne KSeFService z mockiem HTTP (WireMock.Net).
/// Nie wymagają prawdziwego dostępu do KSeF API.
/// </summary>
public sealed class KSeFServiceIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly KSeFService _sut;
    private readonly KSeFApiClientFactory _factory;

    public KSeFServiceIntegrationTests()
    {
        _server = WireMockServer.Start();

        // Nadpisujemy bazowy URL na lokalny mock server
        // W prawdziwej implementacji KSeFApiClientFactory przyjmuje nadpisany URL
        var loggerFactory = NullLoggerFactory.Instance;
        _factory = new KSeFApiClientFactory(loggerFactory, _server.Url!);
        _sut = new KSeFService(_factory, new KSeFDtoMapper(NullLogger<KSeFDtoMapper>.Instance),
            NullLogger<KSeFService>.Instance);
    }

    [Fact]
    public async Task AuthenticateWithTokenAsync_ValidCredentials_ReturnsSession()
    {
        // Arrange
        SetupChallenge();
        SetupInitToken();

        // Act
        var session = await _sut.AuthenticateWithTokenAsync(
            "1234567890", "test-api-token", KSeFEnvironment.Test);

        // Assert
        session.SessionToken.Should().Be("mock-session-token-xyz");
        session.Nip.Should().Be("1234567890");
        session.Environment.Should().Be(KSeFEnvironment.Test);
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetPurchaseInvoicesAsync_WithResults_ReturnsInvoices()
    {
        // Arrange
        SetupChallenge();
        SetupInitToken();
        SetupInvoiceQuery();
        SetupQueryStatusCompleted(numberOfParts: 1);
        SetupQueryResultPart0();

        var session = await _sut.AuthenticateWithTokenAsync(
            "1234567890", "test-api-token", KSeFEnvironment.Test);

        // Act
        var invoices = new List<InvoiceRecord>();
        await foreach (var inv in _sut.GetPurchaseInvoicesAsync(session,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31)))
        {
            invoices.Add(inv);
        }

        // Assert
        invoices.Should().HaveCount(2);
        invoices[0].KSeFNumber.Should().Be("KSeF-001");
        invoices[0].SellerNip.Should().Be("1111111111");
        invoices[1].KSeFNumber.Should().Be("KSeF-002");
    }

    // =====================================================================
    //  SETUP HELPERS
    // =====================================================================

    private void SetupChallenge()
    {
        _server.Given(Request.Create()
                .WithPath("/online/Session/AuthorizationChallenge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    timestamp = "2025-01-15T10:00:00Z",
                    challenge = "test-challenge-abc123"
                })));
    }

    private void SetupInitToken()
    {
        _server.Given(Request.Create()
                .WithPath("/online/Session/InitToken")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    timestamp = "2025-01-15T10:00:01Z",
                    referenceNumber = "ref-12345",
                    sessionToken = new
                    {
                        token = "mock-session-token-xyz",
                        generatedAt = "2025-01-15T10:00:01Z",
                        ttl = 3600
                    }
                })));
    }

    private void SetupInvoiceQuery()
    {
        _server.Given(Request.Create()
                .WithPath("/online/Query/InvoiceQuery")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    timestamp = "2025-01-15T10:00:02Z",
                    referenceNumber = "query-ref-999",
                    processingCode = 100
                })));
    }

    private void SetupQueryStatusCompleted(int numberOfParts)
    {
        _server.Given(Request.Create()
                .WithPath("/online/Query/QueryStatus/query-ref-999")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    referenceNumber = "query-ref-999",
                    processingCode = 200,
                    processingDescription = "Completed",
                    numberOfParts
                })));
    }

    private void SetupQueryResultPart0()
    {
        _server.Given(Request.Create()
                .WithPath("/online/Query/QueryResult/query-ref-999/0")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    referenceNumber = "query-ref-999",
                    partReferenceNumber = "part-0",
                    invoiceHeaderList = new[]
                    {
                        new
                        {
                            ksefReferenceNumber = "KSeF-001",
                            invoiceReferenceNumber = "FV/001/2025",
                            invoicingDate = "2025-01-10",
                            acquisitionTimestamp = "2025-01-10T08:00:00Z",
                            net = 1000m, vat = 230m, gross = 1230m,
                            currency = "PLN",
                            subjectBy = new
                            {
                                issuedByIdentifier = new { type = "onip", identifier = "1111111111" },
                                issuedByName = new { type = "fn", fullName = "Firma A Sp. z o.o." }
                            }
                        },
                        new
                        {
                            ksefReferenceNumber = "KSeF-002",
                            invoiceReferenceNumber = "FV/002/2025",
                            invoicingDate = "2025-01-15",
                            acquisitionTimestamp = "2025-01-15T09:00:00Z",
                            net = 500m, vat = 115m, gross = 615m,
                            currency = "PLN",
                            subjectBy = new
                            {
                                issuedByIdentifier = new { type = "onip", identifier = "2222222222" },
                                issuedByName = new { type = "fn", fullName = "Firma B S.A." }
                            }
                        }
                    }
                })));
    }

    public void Dispose() => _server.Dispose();
}
