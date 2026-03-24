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

    // ─────────────────────────────────────────────────── DOCUMENT ──

    private static void BuildDocument(IDocumentContainer container, InvoiceRecord inv)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
            page.Header().Element(c => BuildHeader(c, inv));
            page.Content().PaddingTop(8).Element(c => BuildContent(c, inv));
            page.Footer().Element(c => BuildFooter(c, inv));
        });
    }

    // ─────────────────────────────────────────────────── HEADER ──

    private static void BuildHeader(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            // Górny pasek — KSeF branding + nr faktury
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Krajowy System e-Faktur")
                        .FontSize(16).Bold().FontColor(Colors.Red.Medium);
                });
                row.ConstantItem(240).Column(c =>
                {
                    c.Item().AlignRight().Text("Numer Faktury:").FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().AlignRight().Text(inv.InvoiceNumber).FontSize(15).Bold();
                    c.Item().AlignRight().Text(InvoiceTypeLabel(inv.InvoiceType)).FontSize(8);
                    c.Item().AlignRight()
                        .Text($"Numer KSEF:{inv.KSeFNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    // ─────────────────────────────────────────────────── CONTENT ──

    private static void BuildContent(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // 1. Sprzedawca / Nabywca
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c => BuildPartyBox(c, "Sprzedawca",
                    inv.SellerNip, inv.SellerName, inv.SellerStreet, inv.SellerPostCode, inv.SellerCity, inv.SellerCountry));
                row.ConstantItem(10);
                row.RelativeItem().Column(c => BuildPartyBox(c, "Nabywca",
                    inv.BuyerNip, inv.BuyerName, inv.BuyerStreet, inv.BuyerPostCode, inv.BuyerCity, inv.BuyerCountry));
            });

            // 2. Szczegóły
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("Szczegóły").Bold().FontSize(10);
                c.Item().Height(4);
                c.Item().Row(r =>
                {
                    r.RelativeItem().Column(left =>
                    {
                        left.Item().Text(text =>
                        {
                            text.Span("Data wystawienia, z zastrzeżeniem art. 106na ust. 1 ustawy: ")
                                .FontSize(8);
                            text.Span($"{inv.IssueDate:dd.MM.yyyy}").FontSize(8).Bold();
                        });
                        if (inv.SaleDate.HasValue)
                            left.Item().Text(text =>
                            {
                                text.Span("Data dokonania lub zakończenia dostawy towarów lub wykonania usługi: ")
                                    .FontSize(8);
                                text.Span($"{inv.SaleDate:dd.MM.yyyy}").FontSize(8).Bold();
                            });
                    });
                    if (!string.IsNullOrEmpty(inv.PlaceOfIssue))
                        r.ConstantItem(180).Text(text =>
                        {
                            text.Span("Miejsce wystawienia: ").FontSize(8);
                            text.Span(inv.PlaceOfIssue).FontSize(8).Bold();
                        });
                });

                // Adnotacje — widoczne tylko gdy aktywne
                var flags = new List<string>();
                if (inv.IsSplitPayment)   flags.Add("Mechanizm podzielonej płatności");
                if (inv.IsReverseCharge)  flags.Add("Odwrotne obciążenie");
                if (inv.IsCashAccounting) flags.Add("Metoda kasowa");
                if (inv.IsSelfInvoicing)  flags.Add("Samofakturowanie");
                if (flags.Count > 0)
                {
                    c.Item().Height(4);
                    c.Item().Background(Colors.Orange.Lighten4).Padding(4)
                        .Text(string.Join("   |   ", flags)).FontSize(8).Bold();
                }
            });

            // Błąd parsowania XML — widoczne ostrzeżenie
            if (!string.IsNullOrEmpty(inv.ParseError))
            {
                col.Item().Background(Colors.Red.Lighten4).Border(0.5f).BorderColor(Colors.Red.Medium)
                    .Padding(6).Column(c =>
                    {
                        c.Item().Text("Błąd odczytu XML faktury — dane mogą być niekompletne")
                            .Bold().FontSize(8).FontColor(Colors.Red.Darken2);
                        c.Item().Text(inv.ParseError).FontSize(7).FontColor(Colors.Red.Darken1);
                    });
            }

            // 3. Pozycje
            col.Item().Column(c =>
            {
                c.Item().Text("Pozycje").Bold().FontSize(10);
                c.Item().Height(4);
                c.Item().Text($"Faktura wystawiona w cenach netto w walucie {inv.Currency}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Height(4);
                c.Item().Element(e => BuildLineItemsTable(e, inv));

                // Tabela identyfikatorów (GTIN/CN/Indeks) — jeśli są dane
                if (inv.LineItems.Any(i => !string.IsNullOrEmpty(i.Gtin)
                    || !string.IsNullOrEmpty(i.CnCode) || !string.IsNullOrEmpty(i.ProductIndex)))
                {
                    c.Item().Height(6);
                    c.Item().Element(e => BuildProductCodesTable(e, inv));
                }

                // Kwota należności ogółem
                c.Item().Height(6);
                c.Item().AlignRight()
                    .Text($"Kwota należności ogółem: {inv.TotalGrossValue:N2} {inv.Currency}")
                    .Bold().FontSize(11);
            });

            // 4. Podsumowanie stawek
            col.Item().Column(c =>
            {
                c.Item().Text("Podsumowanie stawek podatku").Bold().FontSize(10);
                c.Item().Height(4);
                c.Item().Element(e => BuildVatSummaryTable(e, inv));
            });

            // 5. Dodatkowe informacje
            if (inv.AdditionalNotes.Count > 0)
            {
                col.Item().Column(c =>
                {
                    c.Item().Text("Dodatkowe informacje").Bold().FontSize(10);
                    c.Item().Height(4);
                    c.Item().Text("Dodatkowy opis").FontSize(8).Bold();
                    c.Item().Height(2);
                    c.Item().Element(e => BuildAdditionalNotesTable(e, inv.AdditionalNotes));
                });
            }

            // 6. Płatność
            col.Item().Column(c =>
            {
                c.Item().Text("Płatność").Bold().FontSize(10);
                c.Item().Height(4);
                c.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(p =>
                {
                    if (inv.IsPaid)
                    {
                        p.Item().Text("Informacja o płatności: Zapłacono").FontSize(9).Bold();
                        if (inv.PaymentDate.HasValue)
                            p.Item().Text($"Data zapłaty: {inv.PaymentDate:dd.MM.yyyy}").FontSize(9);
                    }
                    if (!string.IsNullOrEmpty(inv.PaymentMethod))
                        p.Item().Text($"Forma płatności: {PaymentMethodLabel(inv.PaymentMethod)}").FontSize(9);
                    if (!inv.IsPaid && inv.PaymentDueDate.HasValue)
                        p.Item().Text($"Termin płatności: {inv.PaymentDueDate:dd.MM.yyyy}").FontSize(9).Bold();
                    foreach (var iban in inv.BankAccountNumbers)
                        p.Item().Text($"Rachunek bankowy: {FormatIban(iban)}").FontSize(9).FontFamily("Courier New");
                    if (!string.IsNullOrEmpty(inv.ContractNumber))
                        p.Item().Text($"Nr umowy: {inv.ContractNumber}").FontSize(9);
                });
            });
        });
    }

    // ─────────────────────────────────────────────────── TABLES ──

    private static void BuildLineItemsTable(IContainer container, InvoiceRecord inv)
    {
        bool hasUuId   = inv.LineItems.Any(i => !string.IsNullOrEmpty(i.UuId));
        bool hasDisc   = inv.LineItems.Any(i => i.DiscountAmount != 0);
        bool hasGtu    = inv.LineItems.Any(i => !string.IsNullOrEmpty(i.GtuCode));
        bool hasP9B    = inv.LineItems.Any(i => i.UnitPriceGross != 0);
        bool hasP11Vat = inv.LineItems.Any(i => i.VatAmountLine != 0);

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);   // Lp
                if (hasUuId) cols.ConstantColumn(40); // UU_ID
                cols.RelativeColumn(3);    // Nazwa
                cols.ConstantColumn(48);   // Cena netto
                if (hasP9B) cols.ConstantColumn(48); // Cena brutto
                cols.ConstantColumn(38);   // Ilość
                cols.ConstantColumn(28);   // J.m.
                cols.ConstantColumn(30);   // Stawka
                cols.ConstantColumn(52);   // Wart. netto
                cols.ConstantColumn(52);   // Wart. brutto
                if (hasP11Vat) cols.ConstantColumn(48); // Wart. VAT
                if (hasGtu) cols.ConstantColumn(35);  // GTU
            });

            static IContainer Th(IContainer c) =>
                c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                 .Padding(3).DefaultTextStyle(x => x.FontSize(7.5f).Bold());

            table.Header(h =>
            {
                h.Cell().Element(Th).Text("Lp.");
                if (hasUuId) h.Cell().Element(Th).Text("Unikalny\nnumer wiersza");
                h.Cell().Element(Th).Text("Nazwa towaru\nlub usługi");
                h.Cell().Element(Th).AlignRight().Text("Cena\njedn.\nnetto");
                if (hasP9B) h.Cell().Element(Th).AlignRight().Text("Cena jedn.\nbrutto");
                h.Cell().Element(Th).AlignRight().Text("Ilość");
                h.Cell().Element(Th).Text("Miara");
                h.Cell().Element(Th).AlignCenter().Text("Stawka\npodatku");
                h.Cell().Element(Th).AlignRight().Text("Wartość\nsprzedaży netto");
                h.Cell().Element(Th).AlignRight().Text("Wartość\nsprzedaży brutto");
                if (hasP11Vat) h.Cell().Element(Th).AlignRight().Text("Wartość\nsprzedaży vat");
                if (hasGtu) h.Cell().Element(Th).AlignCenter().Text("GTU");
            });

            static IContainer Td(IContainer c) =>
                c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

            foreach (var item in inv.LineItems)
            {
                table.Cell().Element(Td).Text($"{item.LineNumber}");
                if (hasUuId) table.Cell().Element(Td).Text(item.UuId);
                table.Cell().Element(Td).Column(c =>
                {
                    c.Item().Text(item.Name);
                    foreach (var kv in item.AdditionalDescriptions)
                        c.Item().Text($"  {kv.Key}: {kv.Value}").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(Td).AlignRight().Text($"{item.UnitPriceNet:N2}");
                if (hasP9B) table.Cell().Element(Td).AlignRight()
                    .Text(item.UnitPriceGross != 0 ? $"{item.UnitPriceGross:N2}" : "");
                table.Cell().Element(Td).AlignRight()
                    .Text($"{item.Quantity:N5}".TrimEnd('0').TrimEnd(',').TrimEnd('.'));
                table.Cell().Element(Td).Text(item.Unit);
                table.Cell().Element(Td).AlignCenter().Text($"{item.VatRate}%");
                table.Cell().Element(Td).AlignRight().Text($"{item.NetValue:N2}");
                table.Cell().Element(Td).AlignRight().Text($"{item.GrossValue:N2}");
                if (hasP11Vat) table.Cell().Element(Td).AlignRight()
                    .Text(item.VatAmountLine != 0 ? $"{item.VatAmountLine:N2}" : "");
                if (hasGtu) table.Cell().Element(Td).AlignCenter().Text(item.GtuCode);
            }
        });
    }

    private static void BuildProductCodesTable(IContainer container, InvoiceRecord inv)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            static IContainer Th(IContainer c) =>
                c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                 .Padding(3).DefaultTextStyle(x => x.FontSize(7.5f).Bold());
            static IContainer Td(IContainer c) =>
                c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

            table.Header(h =>
            {
                h.Cell().Element(Th).Text("Lp.");
                h.Cell().Element(Th).Text("GTIN");
                h.Cell().Element(Th).Text("CN");
                h.Cell().Element(Th).Text("Indeks");
            });

            foreach (var item in inv.LineItems)
            {
                if (string.IsNullOrEmpty(item.Gtin) && string.IsNullOrEmpty(item.CnCode)
                    && string.IsNullOrEmpty(item.ProductIndex)) continue;
                table.Cell().Element(Td).Text($"{item.LineNumber}");
                table.Cell().Element(Td).Text(item.Gtin);
                table.Cell().Element(Td).Text(item.CnCode);
                table.Cell().Element(Td).Text(item.ProductIndex);
            }
        });
    }

    private static void BuildVatSummaryTable(IContainer container, InvoiceRecord inv)
    {
        // Grupuj pozycje po stawce
        var vatGroups = inv.LineItems
            .GroupBy(i => i.VatRate)
            .Select(g => (
                Rate: g.Key,
                Net: g.Sum(i => i.NetValue),
                Vat: g.Sum(i => i.VatAmountLine != 0 ? i.VatAmountLine : i.VatAmount),
                Gross: g.Sum(i => i.GrossValue)
            ))
            .Where(x => x.Net != 0 || x.Vat != 0)
            .ToList();

        // Jeśli brak pozycji, użyj danych z nagłówka faktury
        if (vatGroups.Count == 0)
        {
            // Dane z XML według stawek
            vatGroups =
            [
                ("23", inv.VatAmount23 != 0 ? inv.TotalNetValue - inv.VatAmount8 - inv.VatAmount5 - inv.VatAmount0 - inv.VatAmountExempt : 0,
                       inv.VatAmount23, inv.VatAmount23 != 0 ? inv.TotalNetValue - inv.VatAmount8 - inv.VatAmount5 - inv.VatAmount0 - inv.VatAmountExempt + inv.VatAmount23 : 0),
                ("8",  inv.VatAmount8  != 0 ? 0 : 0, inv.VatAmount8,  0),
                ("5",  inv.VatAmount5  != 0 ? 0 : 0, inv.VatAmount5,  0),
                ("0",  inv.VatAmount0  != 0 ? 0 : 0, inv.VatAmount0,  0),
            ];
            vatGroups = vatGroups.Where(x => x.Vat != 0).ToList();

            // Jeśli nadal brak danych (XML nie załadowany) — pokaż wiersz zbiorczy z sumy nagłówkowej
            if (vatGroups.Count == 0 && inv.TotalGrossValue != 0)
            {
                vatGroups =
                [
                    ("—", inv.TotalNetValue, inv.TotalVatValue, inv.TotalGrossValue)
                ];
            }
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);
                cols.RelativeColumn(2);
                cols.RelativeColumn(3);
                cols.RelativeColumn(3);
                cols.RelativeColumn(3);
            });

            static IContainer Th(IContainer c) =>
                c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                 .Padding(3).DefaultTextStyle(x => x.FontSize(8).Bold());
            static IContainer Td(IContainer c) =>
                c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

            table.Header(h =>
            {
                h.Cell().Element(Th).Text("Lp.");
                h.Cell().Element(Th).Text("Stawka podatku");
                h.Cell().Element(Th).AlignRight().Text("Kwota netto");
                h.Cell().Element(Th).AlignRight().Text("Kwota podatku");
                h.Cell().Element(Th).AlignRight().Text("Kwota brutto");
            });

            int lp = 1;
            foreach (var g in vatGroups)
            {
                table.Cell().Element(Td).Text($"{lp++}");
                table.Cell().Element(Td).Text(g.Rate == "—" ? "—" : $"{g.Rate}% lub {NextRate(g.Rate)}%");
                table.Cell().Element(Td).AlignRight().Text($"{g.Net:N2}");
                table.Cell().Element(Td).AlignRight().Text($"{g.Vat:N2}");
                table.Cell().Element(Td).AlignRight().Text($"{g.Gross:N2}");
            }
        });
    }

    private static void BuildAdditionalNotesTable(IContainer container,
        IReadOnlyList<KeyValuePair<string, string>> notes)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);
                cols.RelativeColumn(2);
                cols.RelativeColumn(4);
            });

            static IContainer Th(IContainer c) =>
                c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                 .Padding(3).DefaultTextStyle(x => x.FontSize(8).Bold());
            static IContainer Td(IContainer c) =>
                c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

            table.Header(h =>
            {
                h.Cell().Element(Th).Text("Lp.");
                h.Cell().Element(Th).Text("Rodzaj informacji");
                h.Cell().Element(Th).Text("Treść informacji");
            });

            int lp = 1;
            foreach (var kv in notes)
            {
                table.Cell().Element(Td).Text($"{lp++}");
                table.Cell().Element(Td).Text(kv.Key);
                table.Cell().Element(Td).Text(kv.Value);
            }
        });
    }

    // ─────────────────────────────────────────────────── FOOTER ──

    private static void BuildFooter(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Wygenerowano przez KSeF Assistant").FontSize(7).FontColor(Colors.Grey.Medium);
                    if (inv.AcquisitionDate != default)
                        text.Span($"  |  Pobrano z KSeF: {inv.AcquisitionDate:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                });
                row.ConstantItem(100).AlignRight().Text(text =>
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

    // ─────────────────────────────────────────────────── PARTY BOX ──

    private static void BuildPartyBox(ColumnDescriptor col, string title,
        string nip, string name, string street, string postCode, string city, string country)
    {
        col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(10);
            c.Item().Height(4);
            c.Item().Text(text =>
            {
                text.Span("NIP: ").FontSize(8).Bold();
                text.Span(nip).FontSize(8);  // bez formatowania — tak jak w oficjalnym KSeF
            });
            // Nazwa może zawierać \n (np. BDO na osobnej linii)
            var nameLines = name.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            c.Item().Text(text =>
            {
                text.Span("Nazwa: ").FontSize(8).Bold();
                text.Span(nameLines.FirstOrDefault() ?? name).FontSize(8);
            });
            foreach (var extra in nameLines.Skip(1))
                c.Item().Text(extra.Trim()).FontSize(8);
            if (!string.IsNullOrEmpty(street) || !string.IsNullOrEmpty(city))
            {
                c.Item().Height(4);
                c.Item().Text("Adres").FontSize(8).Bold();
                // Format: "Ulica 1, 00-000 Miasto" (jedna linia jak w oficjalnym PDF)
                var adresLine = string.Join(", ",
                    new[] { street, $"{postCode} {city}".Trim() }
                    .Where(s => !string.IsNullOrEmpty(s)));
                if (!string.IsNullOrEmpty(adresLine))
                    c.Item().Text(adresLine).FontSize(8);
                if (!string.IsNullOrEmpty(country))
                    c.Item().Text(country).FontSize(8);
            }
        });
    }

    // ─────────────────────────────────────────────────── HELPERS ──

    private static string InvoiceTypeLabel(string code) => code switch
    {
        "Kor"    or "KorZal" or "KorRoz" => "Korekta faktury",
        "Zal"                             => "Faktura zaliczkowa",
        "Roz"                             => "Faktura rozliczeniowa",
        "Upr"                             => "Faktura uproszczona",
        "VatRr"                           => "Faktura VAT RR",
        _                                 => "Faktura podstawowa"
    };

    private static string PaymentMethodLabel(string code) => code switch
    {
        "1" => "Gotówka",
        "2" => "Karta",
        "3" => "Bon",
        "4" => "Czek",
        "5" => "Kredyt",
        "6" => "Przelew",
        "7" => "Mobilna",
        _   => code
    };

    /// <summary>Drugi wariant stawki VAT wyświetlany w tabeli (np. 23% lub 22%).</summary>
    private static string NextRate(string rate) => rate switch
    {
        "23" => "22",
        "8"  => "7",
        "5"  => "4",
        "0"  => "0",
        _    => rate
    };

    private static string FormatIban(string iban)
    {
        iban = iban.Replace(" ", "");
        return string.Join(" ", Enumerable.Range(0, (iban.Length + 3) / 4)
            .Select(i => iban.Substring(i * 4, Math.Min(4, iban.Length - i * 4))));
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray())
               .Trim('-').Replace(" ", "-");
    }
}
