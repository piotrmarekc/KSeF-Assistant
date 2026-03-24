using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KSeFAssistant.Core.Services;

public sealed class PdfExportService : IPdfExportService
{
    private readonly ILogger<PdfExportService> _logger;

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportService(ILogger<PdfExportService> logger) => _logger = logger;

    public Task<byte[]> GeneratePdfAsync(InvoiceRecord invoice, CancellationToken ct = default)
    {
        try
        {
            var bytes = Document.Create(c => BuildDocument(c, invoice)).GeneratePdf();
            return Task.FromResult(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd generowania PDF dla faktury {KSeFNumber}", invoice.KSeFNumber);
            throw;
        }
    }

    public string GetFileName(InvoiceRecord invoice)
    {
        var safe = SanitizeFileName(invoice.InvoiceNumber);
        return $"{invoice.SellerNip}_{invoice.IssueDate:yyyy-MM-dd}_{safe}.pdf";
    }

    // ──────────────────────────────────────────────── DOCUMENT ──

    private static void BuildDocument(IDocumentContainer container, InvoiceRecord inv)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
            page.Header().Element(c => BuildHeader(c, inv));
            page.Content().Element(c => BuildContent(c, inv));
            page.Footer().Element(c => BuildFooter(c, inv));
        });
    }

    // ──────────────────────────────────────────────── HEADER ──

    private static void BuildHeader(IContainer container, InvoiceRecord inv)
    {
        var title = InvoiceTypeTitle(inv.InvoiceType);
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(title).FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    c.Item().Text($"Nr: {inv.InvoiceNumber}").FontSize(11).Bold();
                    if (!string.IsNullOrEmpty(inv.PlaceOfIssue))
                        c.Item().Text($"Miejsce wystawienia: {inv.PlaceOfIssue}").FontSize(8);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().Text($"Data wystawienia: {inv.IssueDate:dd.MM.yyyy}");
                    if (inv.SaleDate.HasValue)
                        c.Item().Text($"Data sprzedaży: {inv.SaleDate:dd.MM.yyyy}");
                    c.Item().Text($"Nr KSeF: {inv.KSeFNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(inv.InvoiceType))
                        c.Item().Text($"Typ: {InvoiceTypeLabel(inv.InvoiceType)}").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });

            // Flagi adnotacji
            var flags = new List<string>();
            if (inv.IsSplitPayment)  flags.Add("Mechanizm podzielonej płatności");
            if (inv.IsReverseCharge) flags.Add("Odwrotne obciążenie");
            if (inv.IsCashAccounting) flags.Add("Metoda kasowa");
            if (inv.IsSelfInvoicing) flags.Add("Samofakturowanie");
            if (flags.Count > 0)
                col.Item().Background(Colors.Orange.Lighten4).Padding(4)
                   .Text(string.Join("  |  ", flags)).FontSize(8).Bold();

            col.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken3);
        });
    }

    // ──────────────────────────────────────────────── CONTENT ──

    private static void BuildContent(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            col.Spacing(8);

            // Strony: sprzedawca / nabywca
            col.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("SPRZEDAWCA").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(inv.SellerName).Bold();
                    c.Item().Text($"NIP: {FormatNip(inv.SellerNip)}");
                    if (!string.IsNullOrEmpty(inv.SellerStreet))
                        c.Item().Text(inv.SellerStreet);
                    if (!string.IsNullOrEmpty(inv.SellerCity))
                        c.Item().Text($"{inv.SellerPostCode} {inv.SellerCity}");
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("NABYWCA").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(inv.BuyerName).Bold();
                    c.Item().Text($"NIP: {FormatNip(inv.BuyerNip)}");
                    if (!string.IsNullOrEmpty(inv.BuyerStreet))
                        c.Item().Text(inv.BuyerStreet);
                    if (!string.IsNullOrEmpty(inv.BuyerCity))
                        c.Item().Text($"{inv.BuyerPostCode} {inv.BuyerCity}");
                });
            });

            // Tabela pozycji
            if (inv.LineItems.Count > 0)
            {
                bool hasGtu      = inv.LineItems.Any(i => !string.IsNullOrEmpty(i.GtuCode));
                bool hasDiscount = inv.LineItems.Any(i => i.DiscountAmount != 0);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(22);    // Lp
                        cols.RelativeColumn(4);     // Nazwa
                        cols.ConstantColumn(28);    // J.m.
                        cols.ConstantColumn(42);    // Ilość
                        cols.ConstantColumn(58);    // Cena netto
                        if (hasDiscount) cols.ConstantColumn(48); // Rabat
                        cols.ConstantColumn(60);    // Netto
                        cols.ConstantColumn(32);    // VAT%
                        cols.ConstantColumn(55);    // Kw. VAT
                        cols.ConstantColumn(60);    // Brutto
                        if (hasGtu) cols.ConstantColumn(38); // GTU
                    });

                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Blue.Darken3).Padding(3)
                         .DefaultTextStyle(x => x.FontSize(7.5f).Bold().FontColor(Colors.White));

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Lp");
                        h.Cell().Element(HeaderCell).Text("Nazwa towaru/usługi");
                        h.Cell().Element(HeaderCell).Text("J.m.");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Ilość");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Cena netto");
                        if (hasDiscount) h.Cell().Element(HeaderCell).AlignRight().Text("Rabat");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Wart. netto");
                        h.Cell().Element(HeaderCell).AlignCenter().Text("VAT%");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Kw. VAT");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Wart. brutto");
                        if (hasGtu) h.Cell().Element(HeaderCell).AlignCenter().Text("GTU");
                    });

                    static IContainer DataCell(IContainer c) =>
                        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4);

                    foreach (var item in inv.LineItems)
                    {
                        table.Cell().Element(DataCell).Text($"{item.LineNumber}");
                        table.Cell().Element(DataCell).Column(c =>
                        {
                            c.Item().Text(item.Name);
                            foreach (var kv in item.AdditionalDescriptions)
                                c.Item().Text($"  {kv.Key}: {kv.Value}").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                        table.Cell().Element(DataCell).Text(item.Unit);
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.Quantity:N4}".TrimEnd('0').TrimEnd('.'));
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.UnitPriceNet:N2}");
                        if (hasDiscount)
                            table.Cell().Element(DataCell).AlignRight()
                                 .Text(item.DiscountAmount != 0 ? $"{item.DiscountAmount:N2}" : "");
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.NetValue:N2}");
                        table.Cell().Element(DataCell).AlignCenter().Text($"{item.VatRate}%");
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.VatAmount:N2}");
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.GrossValue:N2}");
                        if (hasGtu)
                            table.Cell().Element(DataCell).AlignCenter().Text(item.GtuCode);
                    }
                });
            }
            else
            {
                col.Item().Background(Colors.Grey.Lighten4).Padding(6)
                   .Text("Szczegółowe pozycje faktury dostępne po pobraniu XML z KSeF.")
                   .FontSize(8).FontColor(Colors.Grey.Darken2);
            }

            // Podsumowanie stawek VAT
            col.Item().Row(row =>
            {
                row.RelativeItem(); // przestrzeń
                row.ConstantItem(280).Column(summary =>
                {
                    // Tabela stawek VAT (jeśli dane z XML)
                    if (inv.XmlLoaded && inv.LineItems.Count > 0)
                    {
                        summary.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(75); c.ConstantColumn(75); c.ConstantColumn(75); });
                            static IContainer Th(IContainer c) =>
                                c.Background(Colors.Grey.Lighten3).Padding(3)
                                 .DefaultTextStyle(x => x.FontSize(7.5f).Bold());
                            static IContainer Td(IContainer c) =>
                                c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3);

                            t.Header(h =>
                            {
                                h.Cell().Element(Th).Text("Stawka VAT");
                                h.Cell().Element(Th).AlignRight().Text("Netto");
                                h.Cell().Element(Th).AlignRight().Text("VAT");
                                h.Cell().Element(Th).AlignRight().Text("Brutto");
                            });

                            void VatRow(string label, decimal net, decimal vat)
                            {
                                if (net == 0 && vat == 0) return;
                                t.Cell().Element(Td).Text(label);
                                t.Cell().Element(Td).AlignRight().Text($"{net:N2}");
                                t.Cell().Element(Td).AlignRight().Text($"{vat:N2}");
                                t.Cell().Element(Td).AlignRight().Text($"{net + vat:N2}");
                            }

                            var items = inv.LineItems;
                            VatRow("23%",  items.Where(i => i.VatRate == "23").Sum(i => i.NetValue),  inv.VatAmount23);
                            VatRow("8%",   items.Where(i => i.VatRate == "8").Sum(i => i.NetValue),   inv.VatAmount8);
                            VatRow("5%",   items.Where(i => i.VatRate == "5").Sum(i => i.NetValue),   inv.VatAmount5);
                            VatRow("0%",   items.Where(i => i.VatRate == "0").Sum(i => i.NetValue),   inv.VatAmount0);
                            VatRow("zw.",  items.Where(i => i.VatRate is "zw" or "zwol").Sum(i => i.NetValue), inv.VatAmountExempt);
                            VatRow("np.",  items.Where(i => i.VatRate is "np" or "oo").Sum(i => i.NetValue), 0);
                        });
                        summary.Item().Height(4);
                    }

                    // Suma końcowa
                    summary.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Razem netto:").Bold();
                        r.ConstantItem(85).AlignRight().Text($"{inv.TotalNetValue:N2} {inv.Currency}").Bold();
                    });
                    summary.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Razem VAT:").Bold();
                        r.ConstantItem(85).AlignRight().Text($"{inv.TotalVatValue:N2} {inv.Currency}").Bold();
                    });
                    summary.Item().BorderTop(2).BorderColor(Colors.Blue.Darken3).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("DO ZAPŁATY:").Bold().FontSize(12);
                        r.ConstantItem(85).AlignRight()
                         .Text($"{inv.TotalGrossValue:N2} {inv.Currency}").Bold().FontSize(12);
                    });
                });
            });

            // Płatność
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("WARUNKI PŁATNOŚCI").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                c.Item().Row(r =>
                {
                    if (!string.IsNullOrEmpty(inv.PaymentMethod))
                    {
                        r.RelativeItem().Text($"Forma płatności: {PaymentMethodLabel(inv.PaymentMethod)}");
                    }
                    if (inv.PaymentDueDate.HasValue)
                        r.RelativeItem().Text($"Termin: {inv.PaymentDueDate:dd.MM.yyyy}").Bold();
                });
                foreach (var iban in inv.BankAccountNumbers)
                    c.Item().Text($"Rachunek: {FormatIban(iban)}").FontFamily("Courier New").FontSize(8);
                if (!string.IsNullOrEmpty(inv.ContractNumber))
                    c.Item().Text($"Nr umowy: {inv.ContractNumber}").FontSize(8);
            });

            // Informacje dodatkowe faktury
            if (inv.AdditionalNotes.Count > 0)
            {
                col.Item().Column(c =>
                {
                    c.Item().Text("INFORMACJE DODATKOWE").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    foreach (var kv in inv.AdditionalNotes)
                        c.Item().Text($"{kv.Key}: {kv.Value}").FontSize(8);
                });
            }
        });
    }

    // ──────────────────────────────────────────────── FOOTER ──

    private static void BuildFooter(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Wygenerowano przez KSeF Assistant | ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.Span($"Pobrano: {inv.AcquisitionDate:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                });
                row.ConstantItem(120).AlignRight().Text(text =>
                {
                    text.Span("Strona ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                    text.Span(" z ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
            if (!string.IsNullOrEmpty(inv.InvoiceHash))
                col.Item().Text($"SHA-256: {inv.InvoiceHash}").FontSize(6).FontColor(Colors.Grey.Lighten1);
        });
    }

    // ──────────────────────────────────────────────── HELPERS ──

    private static string InvoiceTypeTitle(string code) => code switch
    {
        "Kor"    or "KorZal" or "KorRoz" => "KOREKTA FAKTURY VAT",
        "Zal"                             => "FAKTURA ZALICZKOWA",
        "Roz"                             => "FAKTURA ROZLICZENIOWA",
        "Upr"                             => "FAKTURA UPROSZCZONA",
        "VatRr"                           => "FAKTURA VAT RR",
        _                                 => "FAKTURA VAT"
    };

    private static string InvoiceTypeLabel(string code) => code switch
    {
        "Vat"    => "Faktura VAT",
        "Zal"    => "Zaliczkowa",
        "Kor"    => "Korekta",
        "Roz"    => "Rozliczeniowa",
        "Upr"    => "Uproszczona",
        "KorZal" => "Korekta zaliczkowej",
        "KorRoz" => "Korekta rozliczeniowej",
        "VatRr"  => "Faktura RR",
        _        => code
    };

    private static string PaymentMethodLabel(string code) => code switch
    {
        "1" => "gotówka",
        "2" => "karta",
        "3" => "bon",
        "4" => "czek",
        "5" => "kredyt",
        "6" => "przelew",
        "7" => "mobilna",
        _   => code
    };

    private static string FormatNip(string nip) =>
        nip.Length == 10 ? $"{nip[..3]}-{nip[3..6]}-{nip[6..8]}-{nip[8..]}" : nip;

    private static string FormatIban(string iban)
    {
        // Formatuj IBAN co 4 znaki dla czytelności
        iban = iban.Replace(" ", "");
        return iban.Length > 4
            ? string.Join(" ", Enumerable.Range(0, (iban.Length + 3) / 4)
                .Select(i => iban.Substring(i * 4, Math.Min(4, iban.Length - i * 4))))
            : iban;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray())
               .Trim('-').Replace(" ", "-");
    }
}
