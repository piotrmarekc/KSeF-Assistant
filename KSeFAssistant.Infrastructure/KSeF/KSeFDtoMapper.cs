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
    /// Tworzy podstawowy InvoiceRecord z metadanych (bez XML — bez pozycji).
    /// </summary>
    public InvoiceRecord MapFromHeader(InvoiceHeaderDto dto)
    {
        return new InvoiceRecord
        {
            KSeFNumber = dto.KSeFReferenceNumber,
            InvoiceNumber = dto.InvoiceReferenceNumber,
            AcquisitionDate = TryParseDateTime(dto.AcquisitionTimestamp),
            IssueDate = TryParseDateOnly(dto.InvoicingDate),
            SellerNip = dto.SubjectBy?.Identifier?.Identifier ?? string.Empty,
            SellerName = dto.SubjectBy?.Name?.FullName ?? dto.SubjectBy?.Name?.TradeName ?? string.Empty,
            BuyerNip = dto.SubjectTo?.Identifier?.Identifier ?? string.Empty,
            BuyerName = dto.SubjectTo?.Name?.FullName ?? dto.SubjectTo?.Name?.TradeName ?? string.Empty,
            TotalNetValue = dto.Net,
            TotalGrossValue = dto.Gross,
            Currency = dto.Currency,
            XmlLoaded = false
        };
    }

    /// <summary>
    /// Parsuje XML FA_v3 i uzupełnia InvoiceRecord o szczegółowe dane i pozycje.
    /// </summary>
    public InvoiceRecord EnrichFromXml(InvoiceRecord invoice, string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            // FA_v3 namespace
            ns.AddNamespace("fa", "http://crd.gov.pl/wzor/2023/06/29/12648/");

            string Get(string xpath) =>
                doc.SelectSingleNode(xpath, ns)?.InnerText?.Trim() ?? string.Empty;

            DateOnly GetDate(string xpath) => TryParseDateOnly(Get(xpath));
            decimal GetDecimal(string xpath) => TryParseDecimal(Get(xpath));

            var lineItems = ParseLineItems(doc, ns);

            return invoice with
            {
                InvoiceNumber = Get("//fa:Fa/fa:P_2").IfEmpty(invoice.InvoiceNumber),
                IssueDate     = GetDate("//fa:Fa/fa:P_1").IfDefault(invoice.IssueDate),
                SaleDate      = TryParseDateOnlyNullable(Get("//fa:Fa/fa:P_6")),

                SellerNip      = Get("//fa:Podmiot1/fa:DaneIdentyfikacyjne/fa:NIP").IfEmpty(invoice.SellerNip),
                SellerName     = Get("//fa:Podmiot1/fa:DaneIdentyfikacyjne/fa:Nazwa").IfEmpty(invoice.SellerName),
                SellerStreet   = Get("//fa:Podmiot1/fa:Adres/fa:AdresL1"),
                SellerCity     = Get("//fa:Podmiot1/fa:Adres/fa:Miejscowosc"),
                SellerPostCode = Get("//fa:Podmiot1/fa:Adres/fa:KodPocztowy"),

                BuyerNip      = Get("//fa:Podmiot2/fa:DaneIdentyfikacyjne/fa:NIP").IfEmpty(invoice.BuyerNip),
                BuyerName     = Get("//fa:Podmiot2/fa:DaneIdentyfikacyjne/fa:Nazwa").IfEmpty(invoice.BuyerName),
                BuyerStreet   = Get("//fa:Podmiot2/fa:Adres/fa:AdresL1"),
                BuyerCity     = Get("//fa:Podmiot2/fa:Adres/fa:Miejscowosc"),
                BuyerPostCode = Get("//fa:Podmiot2/fa:Adres/fa:KodPocztowy"),

                TotalNetValue  = GetDecimal("//fa:Fa/fa:P_15").IfZero(invoice.TotalNetValue),
                VatAmount23    = GetDecimal("//fa:Fa/fa:P_14_1"),
                VatAmount8     = GetDecimal("//fa:Fa/fa:P_14_2"),
                VatAmount5     = GetDecimal("//fa:Fa/fa:P_14_3"),
                VatAmount0     = GetDecimal("//fa:Fa/fa:P_14_4"),
                VatAmountExempt = GetDecimal("//fa:Fa/fa:P_14_5"),
                TotalGrossValue = GetDecimal("//fa:Fa/fa:P_15").IfZero(invoice.TotalGrossValue),

                Currency      = Get("//fa:Fa/fa:KodWaluty").IfEmpty(invoice.Currency),
                PaymentMethod = Get("//fa:Fa/fa:P_19"),
                PaymentDueDate = TryParseDateOnlyNullable(Get("//fa:Fa/fa:P_20")),

                LineItems = lineItems,
                XmlLoaded = true,
                ParseError = null
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
                NetValue      = TryParseDecimal(Get("P_11")),
                GrossValue    = TryParseDecimal(Get("P_11A")),
                VatRate       = Get("P_12"),
                VatAmount     = TryParseDecimal(Get("P_14"))
            });
        }
        return items;
    }

    private static DateTime TryParseDateTime(string s)
    {
        if (DateTime.TryParse(s, out var dt)) return dt.ToUniversalTime();
        return DateTime.MinValue;
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
