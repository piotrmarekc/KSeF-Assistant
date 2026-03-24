using FluentAssertions;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    private readonly string _testCertBase64;

    public KSeFServiceIntegrationTests()
    {
        _server = WireMockServer.Start();

        // Generujemy testowy certyfikat RSA (self-signed) — klient zaszyfruje nim token
        using var rsa = RSA.Create(2048);
        var certReq = new CertificateRequest("cn=KSeFTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        _testCertBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var loggerFactory = NullLoggerFactory.Instance;
        var factory = new KSeFApiClientFactory(loggerFactory, _server.Url!);
        _sut = new KSeFService(factory, new KSeFDtoMapper(NullLogger<KSeFDtoMapper>.Instance),
            NullLogger<KSeFService>.Instance);
    }

    [Fact]
    public async Task AuthenticateWithTokenAsync_ValidCredentials_ReturnsSession()
    {
        // Arrange
        SetupPublicKeyCertificates();
        SetupAuthChallenge();
        SetupKsefToken();
        SetupAuthStatus();
        SetupTokenRedeem();

        // Act
        var session = await _sut.AuthenticateWithTokenAsync(
            "1234567890", "test-api-token", KSeFEnvironment.Test);

        // Assert
        session.AccessToken.Should().Be("mock-access-token");
        session.Nip.Should().Be("1234567890");
        session.Environment.Should().Be(KSeFEnvironment.Test);
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetPurchaseInvoicesAsync_WithResults_ReturnsInvoices()
    {
        // Arrange
        SetupPublicKeyCertificates();
        SetupAuthChallenge();
        SetupKsefToken();
        SetupAuthStatus();
        SetupTokenRedeem();
        SetupInvoiceMetadata();

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

    private void SetupPublicKeyCertificates()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/security/public-key-certificates")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    certificates = new[]
                    {
                        new
                        {
                            certificate = _testCertBase64,
                            validFrom = DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),
                            validTo = DateTimeOffset.UtcNow.AddYears(1).ToString("o"),
                            usage = new[] { "KsefTokenEncryption" }
                        }
                    }
                })));
    }

    private void SetupAuthChallenge()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/auth/challenge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    challenge = "test-challenge-abc123",
                    timestamp = "2025-01-15T10:00:00Z",
                    timestampMs = 1736935200000L
                })));
    }

    private void SetupKsefToken()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/auth/ksef-token")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    referenceNumber = "auth-ref-12345",
                    authenticationToken = new
                    {
                        token = "mock-auth-token",
                        validUntil = DateTimeOffset.UtcNow.AddMinutes(5).ToString("o")
                    }
                })));
    }

    private void SetupAuthStatus()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/auth/auth-ref-12345")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    status = new { code = 200, description = "OK" }
                })));
    }

    private void SetupTokenRedeem()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/auth/token/redeem")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    accessToken = new
                    {
                        token = "mock-access-token",
                        validUntil = DateTimeOffset.UtcNow.AddHours(1).ToString("o")
                    },
                    refreshToken = new
                    {
                        token = "mock-refresh-token",
                        validUntil = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
                    }
                })));
    }

    private void SetupInvoiceMetadata()
    {
        _server.Given(Request.Create()
                .WithPath("/v2/invoices/query/metadata")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    hasMore = false,
                    isTruncated = false,
                    invoices = new[]
                    {
                        new
                        {
                            ksefNumber = "KSeF-001",
                            invoiceNumber = "FV/001/2025",
                            issueDate = "2025-01-10T00:00:00Z",
                            acquisitionDate = "2025-01-10T08:00:00Z",
                            netAmount = 1000m,
                            vatAmount = 230m,
                            grossAmount = 1230m,
                            currency = "PLN",
                            seller = new { nip = "1111111111", name = "Firma A Sp. z o.o." },
                            buyer  = new { nip = "9876543210", name = "Nabywca S.A." }
                        },
                        new
                        {
                            ksefNumber = "KSeF-002",
                            invoiceNumber = "FV/002/2025",
                            issueDate = "2025-01-15T00:00:00Z",
                            acquisitionDate = "2025-01-15T09:00:00Z",
                            netAmount = 500m,
                            vatAmount = 115m,
                            grossAmount = 615m,
                            currency = "PLN",
                            seller = new { nip = "2222222222", name = "Firma B S.A." },
                            buyer  = new { nip = "9876543210", name = "Nabywca S.A." }
                        }
                    }
                })));
    }

    public void Dispose() => _server.Dispose();
}
