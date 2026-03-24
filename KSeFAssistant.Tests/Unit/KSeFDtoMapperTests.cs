using FluentAssertions;
using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF;
using KSeFAssistant.Infrastructure.KSeF.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KSeFAssistant.Tests.Unit;

public sealed class KSeFDtoMapperTests
{
    private readonly KSeFDtoMapper _sut = new(NullLogger<KSeFDtoMapper>.Instance);

    [Fact]
    public void MapFromInvoiceSummary_ValidDto_MapsBasicFields()
    {
        var dto = new InvoiceSummaryDto
        {
            KsefNumber = "KSeF-12345",
            InvoiceNumber = "FV/001/2025",
            IssueDate = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
            AcquisitionDate = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero),
            NetAmount = 1000m,
            GrossAmount = 1230m,
            VatAmount = 230m,
            Currency = "PLN",
            Seller = new InvoiceMetadataSellerDto { Nip = "1234567890", Name = "Firma Testowa Sp. z o.o." },
            Buyer  = new InvoiceMetadataBuyerDto
            {
                Identifier = new InvoiceBuyerIdentifierDto { Type = "Nip", Value = "9876543210" },
                Name = "Nabywca S.A."
            }
        };

        var result = _sut.MapFromInvoiceSummary(dto);

        result.KSeFNumber.Should().Be("KSeF-12345");
        result.InvoiceNumber.Should().Be("FV/001/2025");
        result.IssueDate.Should().Be(new DateOnly(2025, 1, 15));
        result.SellerNip.Should().Be("1234567890");
        result.SellerName.Should().Be("Firma Testowa Sp. z o.o.");
        result.BuyerNip.Should().Be("9876543210");
        result.TotalNetValue.Should().Be(1000m);
        result.TotalVatValue.Should().Be(230m);
        result.TotalGrossValue.Should().Be(1230m);
        result.Currency.Should().Be("PLN");
        result.XmlLoaded.Should().BeFalse();
    }

    [Fact]
    public void EnrichFromXml_ValidFaV3Xml_ParsesAllFields()
    {
        var xml = BuildSampleFaV3Xml();
        var baseInvoice = new InvoiceRecord
        {
            KSeFNumber = "KSeF-001",
            InvoiceNumber = "FV/001",
            IssueDate = new DateOnly(2025, 1, 1),
            AcquisitionDate = DateTime.UtcNow
        };

        var result = _sut.EnrichFromXml(baseInvoice,xml);

        result.InvoiceNumber.Should().Be("FV/001/2025");
        result.SellerNip.Should().Be("1234567890");
        result.SellerName.Should().Be("Firma Testowa Sp. z o.o.");
        result.TotalNetValue.Should().Be(1000.00m);
        result.VatAmount23.Should().Be(230.00m);
        result.TotalGrossValue.Should().Be(1230.00m);
        result.Currency.Should().Be("PLN");
        result.LineItems.Should().HaveCount(1);
        result.LineItems[0].Name.Should().Be("Usługa programistyczna");
        result.XmlLoaded.Should().BeTrue();
        result.ParseError.Should().BeNull();
    }

    [Fact]
    public void EnrichFromXml_InvalidXml_SetsParseError()
    {
        var baseInvoice = new InvoiceRecord
        {
            KSeFNumber = "KSeF-001",
            InvoiceNumber = "FV/001",
            IssueDate = new DateOnly(2025, 1, 1),
            AcquisitionDate = DateTime.UtcNow
        };

        var result = _sut.EnrichFromXml(baseInvoice,"<invalid xml");

        result.XmlLoaded.Should().BeTrue();
        result.ParseError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EnrichFromXml_Ns2025_ParsesFaWiersz()
    {
        var baseInvoice = new InvoiceRecord { KSeFNumber = "9930416337-20260322-59A620000001-62" };
        var xml = BuildRealKsefXml2025();

        var result = _sut.EnrichFromXml(baseInvoice, xml);

        result.ParseError.Should().BeNull();
        result.LineItems.Should().HaveCount(1);
        result.LineItems[0].Name.Should().Be("BENZYNA PB95");
        result.LineItems[0].VatRate.Should().Be("23");
        result.VatAmount23.Should().Be(51.71m);
        result.PlaceOfIssue.Should().Be("Wieliczka");
        result.IsPaid.Should().BeTrue();
        result.AdditionalNotes.Should().HaveCount(2);
    }

    private static string BuildRealKsefXml2025() => """
        <Faktura xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://crd.gov.pl/wzor/2025/06/25/13775/">
          <Naglowek>
            <KodFormularza kodSystemowy="FA (3)" wersjaSchemy="1-0E">FA</KodFormularza>
            <WariantFormularza>3</WariantFormularza>
          </Naglowek>
          <Podmiot1>
            <DaneIdentyfikacyjne>
              <NIP>9930416337</NIP>
              <Nazwa>Krak-Tar Sp. z o.o.</Nazwa>
            </DaneIdentyfikacyjne>
            <Adres>
              <KodKraju>PL</KodKraju>
              <AdresL1>Przemyslowa 27, 33-100 Tarnow</AdresL1>
            </Adres>
          </Podmiot1>
          <Podmiot2>
            <DaneIdentyfikacyjne>
              <NIP>6783169728</NIP>
              <Nazwa>CODLAKE SPOLKA AKCYJNA</Nazwa>
            </DaneIdentyfikacyjne>
            <Adres>
              <KodKraju>PL</KodKraju>
              <AdresL1>UL. TESTOWA 14, 31-866 KRAKOW</AdresL1>
            </Adres>
          </Podmiot2>
          <Fa>
            <KodWaluty>PLN</KodWaluty>
            <P_1>2026-03-22</P_1>
            <P_1M>Wieliczka</P_1M>
            <P_2>FV/9/2491/3/2026</P_2>
            <P_6>2026-03-22</P_6>
            <P_13_1>224.81</P_13_1>
            <P_14_1>51.71</P_14_1>
            <P_15>276.52</P_15>
            <Adnotacje>
              <P_16>2</P_16><P_17>2</P_17><P_18>2</P_18><P_18A>2</P_18A>
            </Adnotacje>
            <RodzajFaktury>VAT</RodzajFaktury>
            <DodatkowyOpis>
              <Klucz>Pojazd</Klucz>
              <Wartosc>DX78384</Wartosc>
            </DodatkowyOpis>
            <DodatkowyOpis>
              <Klucz>Wystawil</Klucz>
              <Wartosc>Lukasz Porebski</Wartosc>
            </DodatkowyOpis>
            <FaWiersz>
              <NrWierszaFa>1</NrWierszaFa>
              <UU_ID>34795</UU_ID>
              <P_7>BENZYNA PB95</P_7>
              <Indeks>2</Indeks>
              <GTIN>1111140000047</GTIN>
              <CN>01012910</CN>
              <P_8A>litr</P_8A>
              <P_8B>39.56000</P_8B>
              <P_9A>5.68</P_9A>
              <P_9B>6.99</P_9B>
              <P_11>224.81</P_11>
              <P_11A>276.52</P_11A>
              <P_11Vat>51.71</P_11Vat>
              <P_12>23</P_12>
            </FaWiersz>
            <Platnosc>
              <Zaplacono>1</Zaplacono>
              <DataZaplaty>2026-03-22</DataZaplaty>
              <FormaPlatnosci>2</FormaPlatnosci>
            </Platnosc>
          </Fa>
        </Faktura>
        """;

    private static string BuildSampleFaV3Xml() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <Faktura xmlns="http://crd.gov.pl/wzor/2023/06/29/12648/">
          <Naglowek>
            <KodFormularza kodSystemowy="FA (3)" wersjaSchemy="1-0E">FA</KodFormularza>
            <WariantFormularza>3</WariantFormularza>
          </Naglowek>
          <Podmiot1>
            <DaneIdentyfikacyjne>
              <NIP>1234567890</NIP>
              <Nazwa>Firma Testowa Sp. z o.o.</Nazwa>
            </DaneIdentyfikacyjne>
            <Adres>
              <AdresL1>ul. Testowa 1</AdresL1>
              <Miejscowosc>Warszawa</Miejscowosc>
              <KodPocztowy>00-001</KodPocztowy>
            </Adres>
          </Podmiot1>
          <Podmiot2>
            <DaneIdentyfikacyjne>
              <NIP>9876543210</NIP>
              <Nazwa>Nabywca S.A.</Nazwa>
            </DaneIdentyfikacyjne>
          </Podmiot2>
          <Fa>
            <P_1>2025-01-15</P_1>
            <P_2>FV/001/2025</P_2>
            <P_6>2025-01-15</P_6>
            <P_15>1000.00</P_15>
            <P_14_1>230.00</P_14_1>
            <KodWaluty>PLN</KodWaluty>
            <FaWiersz>
              <NrWierszaFa>1</NrWierszaFa>
              <P_7>Usługa programistyczna</P_7>
              <P_8A>godz.</P_8A>
              <P_8B>10</P_8B>
              <P_9A>100.00</P_9A>
              <P_11>1000.00</P_11>
              <P_11A>1230.00</P_11A>
              <P_12>23</P_12>
              <P_14>230.00</P_14>
            </FaWiersz>
          </Fa>
        </Faktura>
        """;
}
