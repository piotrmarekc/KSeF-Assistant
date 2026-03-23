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
        // QuestPDF: darmowa licencja dla firm < $1M przychodu lub użytek niekomercyjny
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportService(ILogger<PdfExportService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> GeneratePdfAsync(InvoiceRecord invoice, CancellationToken ct = default)
    {
        try
        {
            var bytes = Document.Create(container => BuildDocument(container, invoice))
                               .GeneratePdf();
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
        var safeInvoiceNum = SanitizeFileName(invoice.InvoiceNumber);
        return $"{invoice.SellerNip}_{invoice.IssueDate:yyyy-MM-dd}_{safeInvoiceNum}.pdf";
    }

    private static void BuildDocument(IDocumentContainer container, InvoiceRecord inv)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            page.Header().Element(c => BuildHeader(c, inv));
            page.Content().Element(c => BuildContent(c, inv));
            page.Footer().Element(BuildFooter);
        });
    }

    private static void BuildHeader(IContainer container, InvoiceRecord inv)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("FAKTURA VAT")
                   .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                col.Item().Text($"Nr: {inv.InvoiceNumber}")
                   .FontSize(11).Bold();
            });
            row.ConstantItem(180).Column(col =>
            {
                col.Item().Text($"Data wystawienia: {inv.IssueDate:dd.MM.yyyy}").FontSize(9);
                if (inv.SaleDate.HasValue)
                    col.Item().Text($"Data sprzedaży: {inv.SaleDate:dd.MM.yyyy}").FontSize(9);
                col.Item().Text($"Nr KSeF: {inv.KSeFNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void BuildContent(IContainer container, InvoiceRecord inv)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Dane stron
            col.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("SPRZEDAWCA").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(inv.SellerName).Bold();
                    c.Item().Text($"NIP: {FormatNip(inv.SellerNip)}");
                    c.Item().Text(inv.SellerStreet);
                    c.Item().Text($"{inv.SellerPostCode} {inv.SellerCity}");
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("NABYWCA").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(inv.BuyerName).Bold();
                    c.Item().Text($"NIP: {FormatNip(inv.BuyerNip)}");
                    c.Item().Text(inv.BuyerStreet);
                    c.Item().Text($"{inv.BuyerPostCode} {inv.BuyerCity}");
                });
            });

            // Tabela pozycji
            if (inv.LineItems.Count > 0)
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(25);    // Lp
                        cols.RelativeColumn(4);     // Nazwa
                        cols.ConstantColumn(30);    // J.m.
                        cols.ConstantColumn(45);    // Ilość
                        cols.ConstantColumn(60);    // Cena netto
                        cols.ConstantColumn(35);    // VAT%
                        cols.ConstantColumn(65);    // Netto
                        cols.ConstantColumn(65);    // Brutto
                    });

                    // Nagłówek
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Blue.Darken3).Padding(4)
                         .DefaultTextStyle(x => x.FontSize(8).Bold().FontColor(Colors.White));

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Lp");
                        h.Cell().Element(HeaderCell).Text("Nazwa towaru/usługi");
                        h.Cell().Element(HeaderCell).Text("J.m.");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Ilość");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Cena netto");
                        h.Cell().Element(HeaderCell).AlignCenter().Text("VAT");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Wart. netto");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Wart. brutto");
                    });

                    static IContainer DataCell(IContainer c) =>
                        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                    foreach (var item in inv.LineItems)
                    {
                        table.Cell().Element(DataCell).Text($"{item.LineNumber}");
                        table.Cell().Element(DataCell).Text(item.Name);
                        table.Cell().Element(DataCell).Text(item.Unit);
                        table.Cell().Element(DataCell).AlignRight().Text($"{item.Quantity:N2}");
                        table.Cell().Element(DataCell).AlignRight()
                             .Text($"{item.UnitPriceNet:N2} {inv.Currency}");
                        table.Cell().Element(DataCell).AlignCenter().Text($"{item.VatRate}%");
                        table.Cell().Element(DataCell).AlignRight()
                             .Text($"{item.NetValue:N2} {inv.Currency}");
                        table.Cell().Element(DataCell).AlignRight()
                             .Text($"{item.GrossValue:N2} {inv.Currency}");
                    }
                });
            }

            // Podsumowanie wartości
            col.Item().AlignRight().Width(240).Column(summary =>
            {
                summary.Item().Row(r =>
                {
                    r.RelativeItem().Text("Razem netto:").Bold();
                    r.ConstantItem(90).AlignRight().Text($"{inv.TotalNetValue:N2} {inv.Currency}").Bold();
                });
                if (inv.VatAmount23 != 0)
                    summary.Item().Row(r =>
                    {
                        r.RelativeItem().Text("VAT 23%:");
                        r.ConstantItem(90).AlignRight().Text($"{inv.VatAmount23:N2} {inv.Currency}");
                    });
                if (inv.VatAmount8 != 0)
                    summary.Item().Row(r =>
                    {
                        r.RelativeItem().Text("VAT 8%:");
                        r.ConstantItem(90).AlignRight().Text($"{inv.VatAmount8:N2} {inv.Currency}");
                    });
                if (inv.VatAmount5 != 0)
                    summary.Item().Row(r =>
                    {
                        r.RelativeItem().Text("VAT 5%:");
                        r.ConstantItem(90).AlignRight().Text($"{inv.VatAmount5:N2} {inv.Currency}");
                    });
                summary.Item().BorderTop(2).BorderColor(Colors.Blue.Darken3).Padding(3).Row(r =>
                {
                    r.RelativeItem().Text("RAZEM BRUTTO:").Bold().FontSize(11);
                    r.ConstantItem(90).AlignRight().Text($"{inv.TotalGrossValue:N2} {inv.Currency}")
                     .Bold().FontSize(11);
                });
            });

            // Płatność
            if (!string.IsNullOrEmpty(inv.PaymentMethod))
            {
                col.Item().Text($"Sposób płatności: {inv.PaymentMethod}").FontSize(8);
                if (inv.PaymentDueDate.HasValue)
                    col.Item().Text($"Termin płatności: {inv.PaymentDueDate:dd.MM.yyyy}").FontSize(8);
            }
        });
    }

    private static void BuildFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Wygenerowano przez KSeF Assistant | Strona ").FontSize(7).FontColor(Colors.Grey.Medium);
            text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
            text.Span(" z ").FontSize(7).FontColor(Colors.Grey.Medium);
            text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
        });
    }

    private static string FormatNip(string nip) =>
        nip.Length == 10 ? $"{nip[..3]}-{nip[3..6]}-{nip[6..8]}-{nip[8..]}" : nip;

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray())
               .Trim('-').Replace(" ", "-");
    }
}
