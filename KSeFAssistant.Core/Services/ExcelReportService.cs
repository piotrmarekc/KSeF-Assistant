using ClosedXML.Excel;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace KSeFAssistant.Core.Services;

public sealed class ExcelReportService : IExcelReportService
{
    private readonly ILogger<ExcelReportService> _logger;

    private static readonly XLColor HeaderBg = XLColor.FromHtml("#2E4057");
    private static readonly XLColor HeaderFg = XLColor.White;

    public ExcelReportService(ILogger<ExcelReportService> logger)
    {
        _logger = logger;
    }

    public Task GenerateReportAsync(IReadOnlyList<InvoiceRecord> invoices,
        FilterCriteria criteria, string outputPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Generowanie raportu Excel: {Count} faktur → {Path}",
            invoices.Count, outputPath);

        using var workbook = new XLWorkbook();
        BuildInvoicesSheet(workbook, invoices);
        BuildSummarySheet(workbook, invoices, criteria);

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void BuildInvoicesSheet(XLWorkbook wb, IReadOnlyList<InvoiceRecord> invoices)
    {
        var ws = wb.Worksheets.Add("Faktury");

        // --- Nagłówki ---
        string[] headers =
        [
            "Nr faktury", "Nr KSeF", "Data wystawienia", "Data sprzedaży",
            "NIP sprzedawcy", "Nazwa sprzedawcy",
            "NIP nabywcy", "Nazwa nabywcy",
            "Wartość netto", "VAT 23%", "VAT 8%", "VAT 5%",
            "Wartość brutto", "Waluta",
            "Sposób płatności", "Termin płatności"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = HeaderFg;
            cell.Style.Fill.BackgroundColor = HeaderBg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // --- Dane ---
        int row = 2;
        foreach (var inv in invoices)
        {
            ws.Cell(row, 1).Value = inv.InvoiceNumber;
            ws.Cell(row, 2).Value = inv.KSeFNumber;
            ws.Cell(row, 3).Value = inv.IssueDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 4).Value = inv.SaleDate?.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 5).Value = inv.SellerNip;
            ws.Cell(row, 6).Value = inv.SellerName;
            ws.Cell(row, 7).Value = inv.BuyerNip;
            ws.Cell(row, 8).Value = inv.BuyerName;
            ws.Cell(row, 9).Value = inv.TotalNetValue;
            ws.Cell(row, 10).Value = inv.VatAmount23;
            ws.Cell(row, 11).Value = inv.VatAmount8;
            ws.Cell(row, 12).Value = inv.VatAmount5;
            ws.Cell(row, 13).Value = inv.TotalGrossValue;
            ws.Cell(row, 14).Value = inv.Currency;
            ws.Cell(row, 15).Value = inv.PaymentMethod;
            ws.Cell(row, 16).Value = inv.PaymentDueDate?.ToDateTime(TimeOnly.MinValue);
            row++;
        }

        // --- Formatowanie ---
        // Daty
        ws.Range(2, 3, row - 1, 3).Style.DateFormat.Format = "yyyy-MM-dd";
        ws.Range(2, 4, row - 1, 4).Style.DateFormat.Format = "yyyy-MM-dd";
        ws.Range(2, 16, row - 1, 16).Style.DateFormat.Format = "yyyy-MM-dd";

        // Liczby (wartości)
        var moneyColumns = new[] { 9, 10, 11, 12, 13 };
        foreach (var col in moneyColumns)
            ws.Range(2, col, row - 1, col).Style.NumberFormat.Format = "#,##0.00";

        // Wyrównanie prawne dla kwot
        foreach (var col in moneyColumns)
            ws.Range(1, col, row - 1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // AutoFit + freeze header
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
    }

    private static void BuildSummarySheet(XLWorkbook wb,
        IReadOnlyList<InvoiceRecord> invoices, FilterCriteria criteria)
    {
        var ws = wb.Worksheets.Add("Podsumowanie");

        // Metadane raportu
        ws.Cell("A1").Value = "Raport faktur zakupowych KSeF";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A2").Value = "Okres:";
        ws.Cell("B2").Value = $"{criteria.PeriodStart:dd.MM.yyyy} – {criteria.PeriodEnd:dd.MM.yyyy}";
        ws.Cell("A3").Value = "Wygenerowano:";
        ws.Cell("B3").Value = DateTime.Now;
        ws.Cell("B3").Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
        ws.Cell("A4").Value = "Liczba faktur:";
        ws.Cell("B4").Value = invoices.Count;

        // --- Per dostawca ---
        ws.Cell("A6").Value = "Zestawienie per dostawca";
        ws.Cell("A6").Style.Font.Bold = true;
        ws.Cell("A6").Style.Font.FontSize = 11;

        string[] headers = ["NIP sprzedawcy", "Nazwa sprzedawcy", "Liczba faktur",
                             "Suma netto", "VAT 23%", "VAT 8%", "VAT 5%", "Suma brutto"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(7, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = HeaderFg;
            cell.Style.Fill.BackgroundColor = HeaderBg;
        }

        var grouped = invoices
            .GroupBy(inv => inv.SellerNip, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.First().SellerName)
            .ToList();

        int row = 8;
        foreach (var g in grouped)
        {
            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = g.First().SellerName;
            ws.Cell(row, 3).Value = g.Count();
            ws.Cell(row, 4).Value = g.Sum(x => x.TotalNetValue);
            ws.Cell(row, 5).Value = g.Sum(x => x.VatAmount23);
            ws.Cell(row, 6).Value = g.Sum(x => x.VatAmount8);
            ws.Cell(row, 7).Value = g.Sum(x => x.VatAmount5);
            ws.Cell(row, 8).Value = g.Sum(x => x.TotalGrossValue);
            row++;
        }

        // Wiersz sumy
        ws.Cell(row, 2).Value = "ŁĄCZNIE";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = invoices.Count;
        ws.Cell(row, 4).Value = invoices.Sum(x => x.TotalNetValue);
        ws.Cell(row, 5).Value = invoices.Sum(x => x.VatAmount23);
        ws.Cell(row, 6).Value = invoices.Sum(x => x.VatAmount8);
        ws.Cell(row, 7).Value = invoices.Sum(x => x.VatAmount5);
        ws.Cell(row, 8).Value = invoices.Sum(x => x.TotalGrossValue);
        ws.Range(row, 1, row, 8).Style.Font.Bold = true;
        ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EDF5");

        // Formatowanie kwot
        var moneyRange = ws.Range(8, 4, row, 8);
        moneyRange.Style.NumberFormat.Format = "#,##0.00";
        moneyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(7);
    }
}
