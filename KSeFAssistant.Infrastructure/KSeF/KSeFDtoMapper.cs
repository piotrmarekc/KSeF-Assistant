using KSeFAssistant.Core.Models;
using KSeFAssistant.Infrastructure.KSeF.Dto;
using Microsoft.Extensions.Logging;
using System.Xml;

namespace KSeFAssistant.Infrastructure.KSeF;

/// <summary>
/// Mapuje odpowiedzi API KSeF i XML FA_v3 → domenowy InvoiceRecord.
/// </summary>
public sealed class KSeFDtoMapper
{
    private readonly ILogger<KSeFDtoMapper> _logger;

    public KSeFDtoMapper(ILogger<KSeFDtoMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tworzy InvoiceRecord z metadanych API v2 (bez XML — bez pozycji i szczegółów adresów).
    /// </summary>
    public InvoiceRecord MapFromInvoiceSummary(InvoiceSummaryDto dto)
    {
        return new InvoiceRecord
        {
            KSeFNumber          = dto.KsefNumber,
            InvoiceNumber       = dto.InvoiceNumber,
            InvoiceType         = dto.InvoiceType,
            InvoiceHash         = dto.InvoiceHash,

            IssueDate           = DateOnly.FromDateTime(dto.IssueDate.Date),
            AcquisitionDate     = dto.AcquisitionDate.UtcDateTime,
            InvoicingDate       = dto.InvoicingDate.UtcDateTime,
            PermanentStorageDate = dto.PermanentStorageDate.UtcDateTime,

            SellerNip           = dto.Seller?.Nip ?? string.Empty,
            SellerName          = dto.Seller?.Name ?? string.Empty,

            BuyerNip            = dto.Buyer?.IdentifierValue ?? string.Empty,
            BuyerName           = dto.Buyer?.Name ?? string.Empty,

            TotalNetValue       = dto.NetAmount,
            TotalVatValue       = dto.VatAmount,
            TotalGrossValue     = dto.GrossAmount,
            Currency            = dto.Currency,

            IsSelfInvoicing     = dto.IsSelfInvoicing,
            HasAttachment       = dto.HasAttachment,

            XmlLoaded           = false
        };
    }

    /// <summary>
    /// Parsuje XML FA_v3 i uzupełnia InvoiceRecord o szczegółowe dane, pozycje i adnotacje.
    /// </summary>
    public InvoiceRecord EnrichFromXml(InvoiceRecord invoice, string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("fa", "http://crd.gov.pl/wzor/2023/06/29/12648/");

            string Get(string xpath) =>
                doc.SelectSingleNode(xpath, ns)?.InnerText?.Trim() ?? string.Empty;

            DateOnly GetDate(string xpath) => TryParseDateOnly(Get(xpath));
            decimal GetDecimal(string xpath) => TryParseDecimal(Get(xpath));
            bool GetBool1(string xpath) => Get(xpath) == "1";

            // --- Wartości ---
            var net23 = GetDecimal("//fa:Fa/fa:P_13_1");
            var net8  = GetDecimal("//fa:Fa/fa:P_13_2");
            var net5  = GetDecimal("//fa:Fa/fa:P_13_3");
            var net0  = GetDecimal("//fa:Fa/fa:P_13_4");
            var vat23 = GetDecimal("//fa:Fa/fa:P_14_1");
            var vat8  = GetDecimal("//fa:Fa/fa:P_14_2");
            var vat5  = GetDecimal("//fa:Fa/fa:P_14_3");
            var vat0  = GetDecimal("//fa:Fa/fa:P_14_4");
            var vatEx = GetDecimal("//fa:Fa/fa:P_14_5");
            var totalVat = (vat23 + vat8 + vat5 + vat0 + vatEx).IfZero(invoice.TotalVatValue);
            var totalNet = GetDecimal("//fa:Fa/fa:P_15").IfZero(invoice.TotalNetValue);

            // --- Płatność ---
            var paymentMethod = Get("//fa:Fa/fa:Platnosc/fa:FormaPlatnosci");
            var paymentDue    = TryParseDateOnlyNullable(
                Get("//fa:Fa/fa:Platnosc/fa:TerminPlatnosci[1]/fa:Termin"));
            var bankAccounts  = ParseBankAccounts(doc, ns);

            // --- Adnotacje ---
            var isReverseCharge = GetBool1("//fa:Fa/fa:Adnotacje/fa:P_16");
            var isSelfInv       = GetBool1("//fa:Fa/fa:Adnotacje/fa:P_17");
            var isSplitPay      = GetBool1("//fa:Fa/fa:Adnotacje/fa:P_18");
            var isCashAcc       = GetBool1("//fa:Fa/fa:Adnotacje/fa:P_18A");

            // --- Informacje dodatkowe ---
            var placeOfIssue   = Get("//fa:Fa/fa:P_1M");
            var contractNumber = Get("//fa:Fa/fa:WarunkiTransakcji/fa:Umowy[1]/fa:NrUmowy");
            var additionalNotes = ParseAdditionalDescriptions(doc, ns, "//fa:Fa/fa:DodatkowyOpis");

            return invoice with
            {
                InvoiceNumber   = Get("//fa:Fa/fa:P_2").IfEmpty(invoice.InvoiceNumber),
                IssueDate       = GetDate("//fa:Fa/fa:P_1").IfDefault(invoice.IssueDate),
                SaleDate        = TryParseDateOnlyNullable(Get("//fa:Fa/fa:P_6")),
                PlaceOfIssue    = placeOfIssue,

                SellerNip       = Get("//fa:Podmiot1/fa:DaneIdentyfikacyjne/fa:NIP").IfEmpty(invoice.SellerNip),
                SellerName      = Get("//fa:Podmiot1/fa:DaneIdentyfikacyjne/fa:Nazwa").IfEmpty(invoice.SellerName),
                SellerStreet    = Get("//fa:Podmiot1/fa:Adres/fa:AdresL1"),
                SellerCity      = Get("//fa:Podmiot1/fa:Adres/fa:Miejscowosc"),
                SellerPostCode  = Get("//fa:Podmiot1/fa:Adres/fa:KodPocztowy"),

                BuyerNip        = Get("//fa:Podmiot2/fa:DaneIdentyfikacyjne/fa:NIP").IfEmpty(invoice.BuyerNip),
                BuyerName       = Get("//fa:Podmiot2/fa:DaneIdentyfikacyjne/fa:Nazwa").IfEmpty(invoice.BuyerName),
                BuyerStreet     = Get("//fa:Podmiot2/fa:Adres/fa:AdresL1"),
                BuyerCity       = Get("//fa:Podmiot2/fa:Adres/fa:Miejscowosc"),
                BuyerPostCode   = Get("//fa:Podmiot2/fa:Adres/fa:KodPocztowy"),

                TotalNetValue   = totalNet,
                VatAmount23     = vat23,
                VatAmount8      = vat8,
                VatAmount5      = vat5,
                VatAmount0      = vat0,
                VatAmountExempt = vatEx,
                TotalVatValue   = totalVat,
                TotalGrossValue = (totalNet + totalVat).IfZero(invoice.TotalGrossValue),

                Currency        = Get("//fa:Fa/fa:KodWaluty").IfEmpty(invoice.Currency),
                PaymentMethod   = paymentMethod,
                PaymentDueDate  = paymentDue,
                BankAccountNumbers = bankAccounts,

                IsReverseCharge = isReverseCharge,
                IsSplitPayment  = isSplitPay,
                IsCashAccounting = isCashAcc,
                IsSelfInvoicing = isSelfInv || invoice.IsSelfInvoicing,

                ContractNumber  = contractNumber,
                AdditionalNotes = additionalNotes,

                LineItems       = ParseLineItems(doc, ns),
                XmlLoaded       = true,
                ParseError      = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd parsowania XML FA_v3 dla faktury {KSeFNumber}", invoice.KSeFNumber);
            return invoice with { XmlLoaded = true, ParseError = ex.Message };
        }
    }

    private static IReadOnlyList<InvoiceLineItem> ParseLineItems(XmlDocument doc, XmlNamespaceManager ns)
    {
        var nodes = doc.SelectNodes("//fa:FaWiersz", ns);
        if (nodes is null || nodes.Count == 0) return [];

        var items = new List<InvoiceLineItem>(nodes.Count);
        foreach (XmlNode node in nodes)
        {
            string Get(string name) =>
                node.SelectSingleNode($"fa:{name}", ns)?.InnerText?.Trim() ?? string.Empty;

            items.Add(new InvoiceLineItem
            {
                LineNumber    = int.TryParse(Get("NrWierszaFa"), out var lp) ? lp : items.Count + 1,
                Name          = Get("P_7"),
                Unit          = Get("P_8A"),
                Quantity      = TryParseDecimal(Get("P_8B")),
                UnitPriceNet  = TryParseDecimal(Get("P_9A")),
                DiscountAmount = TryParseDecimal(Get("P_10")),
                NetValue      = TryParseDecimal(Get("P_11")),
                GrossValue    = TryParseDecimal(Get("P_11A")),
                VatRate       = Get("P_12"),
                VatAmount     = TryParseDecimal(Get("P_14")),
                GtuCode       = Get("GTU"),
                AdditionalDescriptions = ParseAdditionalDescriptions(node, ns, "fa:DodatkowyOpis")
            });
        }
        return items;
    }

    private static IReadOnlyList<string> ParseBankAccounts(XmlDocument doc, XmlNamespaceManager ns)
    {
        var nodes = doc.SelectNodes("//fa:Fa/fa:Platnosc/fa:RachunekBankowy", ns);
        if (nodes is null || nodes.Count == 0) return [];

        var result = new List<string>(nodes.Count);
        foreach (XmlNode node in nodes)
        {
            // Numer rachunku może być w NrRB lub NrRachunku zależnie od wariantu
            var nr = node.SelectSingleNode("fa:NrRB", ns)?.InnerText?.Trim()
                  ?? node.SelectSingleNode("fa:NrRachunku", ns)?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(nr))
                result.Add(nr);
        }
        return result;
    }

    /// <summary>Parsuje elementy DodatkowyOpis (klucz-wartość) z danego węzła lub ścieżki.</summary>
    private static IReadOnlyList<KeyValuePair<string, string>> ParseAdditionalDescriptions(
        XmlNode parent, XmlNamespaceManager ns, string xpath)
    {
        var nodes = parent.SelectNodes(xpath, ns);
        if (nodes is null || nodes.Count == 0) return [];

        var result = new List<KeyValuePair<string, string>>(nodes.Count);
        foreach (XmlNode node in nodes)
        {
            var key   = node.SelectSingleNode("fa:Klucz", ns)?.InnerText?.Trim() ?? string.Empty;
            var value = node.SelectSingleNode("fa:Wartosc", ns)?.InnerText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
                result.Add(new KeyValuePair<string, string>(key, value));
        }
        return result;
    }

    private static DateOnly TryParseDateOnly(string s)
    {
        if (DateOnly.TryParse(s, out var d)) return d;
        if (DateTime.TryParse(s, out var dt)) return DateOnly.FromDateTime(dt);
        return DateOnly.MinValue;
    }

    private static DateOnly? TryParseDateOnlyNullable(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return TryParseDateOnly(s) is { } d && d != DateOnly.MinValue ? d : null;
    }

    private static decimal TryParseDecimal(string s) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
}

file static class StringExtensions
{
    public static string IfEmpty(this string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}

file static class DateOnlyExtensions
{
    public static DateOnly IfDefault(this DateOnly value, DateOnly fallback) =>
        value == DateOnly.MinValue ? fallback : value;
}

file static class DecimalExtensions
{
    public static decimal IfZero(this decimal value, decimal fallback) =>
        value == 0m ? fallback : value;
}
