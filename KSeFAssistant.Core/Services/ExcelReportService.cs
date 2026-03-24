using ClosedXML.Excel;
using KSeFAssistant.Core.Interfaces;
using KSeFAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace KSeFAssistant.Core.Services;

public sealed class ExcelReportService : IExcelReportService
{
    private readonly ILogger<ExcelReportService> _logger;

    private static readonly XLColor HeaderBg   = XLColor.FromHtml("#2E4057");
    private static readonly XLColor HeaderFg   = XLColor.White;
    private static readonly XLColor SubHeaderBg = XLColor.FromHtml("#E8EDF5");
    private static readonly string MoneyFmt    = "#,##0.00";
    private static readonly string DateFmt     = "yyyy-MM-dd";
    private static readonly string DateTimeFmt = "yyyy-MM-dd HH:mm";

    public ExcelReportService(ILogger<ExcelReportService> logger) => _logger = logger;

    public Task GenerateReportAsync(IReadOnlyList<InvoiceRecord> invoices,
        FilterCriteria criteria, string outputPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Generowanie raportu Excel: {Count} faktur → {Path}", invoices.Count, outputPath);

        using var wb = new XLWorkbook();
        BuildSummarySheet(wb, invoices, criteria);
        BuildInvoicesSheet(wb, invoices);
        BuildLineItemsSheet(wb, invoices);
        wb.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    // ────────────────────────────────────────────────────────────── ARKUSZE ──

    private static void BuildInvoicesSheet(XLWorkbook wb, IReadOnlyList<InvoiceRecord> invoices)
    {
        var ws = wb.Worksheets.Add("Faktury");

        string[] headers =
        [
            // Identyfikatory
            "Nr faktury", "Nr KSeF", "Typ faktury",
            // Daty
            "Data wystawienia", "Data sprzedaży", "Data przyjęcia KSeF",
            "Data trwałego zapisu", "Miejsce wystawienia",
            // Sprzedawca
            "NIP sprzedawcy", "Nazwa sprzedawcy",
            "Ulica sprzedawcy", "Miasto sprzedawcy", "Kod pocztowy sprzedawcy",
            // Nabywca
            "NIP nabywcy", "Nazwa nabywcy",
            "Ulica nabywcy", "Miasto nabywcy", "Kod pocztowy nabywcy",
            // Kwoty
            "Netto", "VAT 23%", "VAT 8%", "VAT 5%", "VAT 0%", "VAT zwol.", "Suma VAT", "Brutto",
            "Waluta",
            // Płatność
            "Forma płatności", "Termin płatności", "Rachunek bankowy",
            // Adnotacje / flagi
            "Nr umowy", "Odwrotne obciążenie", "Podzielona płatność",
            "Metoda kasowa", "Samofakturowanie", "Załącznik",
            // Weryfikacja
            "Hash SHA-256"
        ];

        WriteHeaderRow(ws, 1, headers);

        int row = 2;
        foreach (var inv in invoices)
        {
            int c = 1;
            ws.Cell(row, c++).Value = inv.InvoiceNumber;
            ws.Cell(row, c++).Value = inv.KSeFNumber;
            ws.Cell(row, c++).Value = InvoiceTypeLabel(inv.InvoiceType);
            ws.Cell(row, c++).Value = inv.IssueDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, c++).Value = inv.SaleDate?.ToDateTime(TimeOnly.MinValue);
            if (inv.InvoicingDate != default)
                ws.Cell(row, c).Value = inv.InvoicingDate;
            c++;
            if (inv.PermanentStorageDate != default)
                ws.Cell(row, c).Value = inv.PermanentStorageDate;
            c++;
            ws.Cell(row, c++).Value = inv.PlaceOfIssue;

            ws.Cell(row, c++).Value = inv.SellerNip;
            ws.Cell(row, c++).Value = inv.SellerName;
            ws.Cell(row, c++).Value = inv.SellerStreet;
            ws.Cell(row, c++).Value = inv.SellerCity;
            ws.Cell(row, c++).Value = inv.SellerPostCode;

            ws.Cell(row, c++).Value = inv.BuyerNip;
            ws.Cell(row, c++).Value = inv.BuyerName;
            ws.Cell(row, c++).Value = inv.BuyerStreet;
            ws.Cell(row, c++).Value = inv.BuyerCity;
            ws.Cell(row, c++).Value = inv.BuyerPostCode;

            ws.Cell(row, c++).Value = inv.TotalNetValue;
            ws.Cell(row, c++).Value = inv.VatAmount23;
            ws.Cell(row, c++).Value = inv.VatAmount8;
            ws.Cell(row, c++).Value = inv.VatAmount5;
            ws.Cell(row, c++).Value = inv.VatAmount0;
            ws.Cell(row, c++).Value = inv.VatAmountExempt;
            ws.Cell(row, c++).Value = inv.TotalVatValue;
            ws.Cell(row, c++).Value = inv.TotalGrossValue;
            ws.Cell(row, c++).Value = inv.Currency;

            ws.Cell(row, c++).Value = inv.PaymentMethod;
            ws.Cell(row, c++).Value = inv.PaymentDueDate?.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, c++).Value = string.Join("; ", inv.BankAccountNumbers);

            ws.Cell(row, c++).Value = inv.ContractNumber;
            ws.Cell(row, c++).Value = inv.IsReverseCharge ? "TAK" : "";
            ws.Cell(row, c++).Value = inv.IsSplitPayment  ? "TAK" : "";
            ws.Cell(row, c++).Value = inv.IsCashAccounting ? "TAK" : "";
            ws.Cell(row, c++).Value = inv.IsSelfInvoicing  ? "TAK" : "";
            ws.Cell(row, c++).Value = inv.HasAttachment    ? "TAK" : "";
            ws.Cell(row, c++).Value = inv.InvoiceHash ?? "";
            row++;
        }

        int lastRow  = row - 1;
        int lastCol  = headers.Length;

        // Formaty dat
        foreach (int col in new[] { 4, 5, 6, 7, 27 })
            if (lastRow >= 2) ws.Range(2, col, lastRow, col).Style.DateFormat.Format = DateFmt;

        // Formaty kwot (kolumny 19-26)
        for (int col = 19; col <= 26; col++)
            if (lastRow >= 2)
            {
                ws.Range(2, col, lastRow, col).Style.NumberFormat.Format = MoneyFmt;
                ws.Range(2, col, lastRow, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
    }

    private static void BuildLineItemsSheet(XLWorkbook wb, IReadOnlyList<InvoiceRecord> invoices)
    {
        var ws = wb.Worksheets.Add("Pozycje faktur");

        string[] headers =
        [
            "Nr faktury", "Nr KSeF",
            "Lp", "Nazwa towaru/usługi", "J.m.", "Ilość",
            "Cena netto", "Rabat", "Wartość netto", "Wartość brutto",
            "Stawka VAT", "Kwota VAT", "Kod GTU",
            "Opisy dodatkowe"
        ];

        WriteHeaderRow(ws, 1, headers);

        int row = 2;
        foreach (var inv in invoices)
        {
            if (inv.LineItems.Count == 0) continue;
            foreach (var item in inv.LineItems)
            {
                int c = 1;
                ws.Cell(row, c++).Value = inv.InvoiceNumber;
                ws.Cell(row, c++).Value = inv.KSeFNumber;
                ws.Cell(row, c++).Value = item.LineNumber;
                ws.Cell(row, c++).Value = item.Name;
                ws.Cell(row, c++).Value = item.Unit;
                ws.Cell(row, c++).Value = item.Quantity;
                ws.Cell(row, c++).Value = item.UnitPriceNet;
                if (item.DiscountAmount != 0)
                    ws.Cell(row, c).Value = item.DiscountAmount;
                c++;
                ws.Cell(row, c++).Value = item.NetValue;
                ws.Cell(row, c++).Value = item.GrossValue;
                ws.Cell(row, c++).Value = item.VatRate;
                ws.Cell(row, c++).Value = item.VatAmount;
                ws.Cell(row, c++).Value = item.GtuCode;
                ws.Cell(row, c++).Value = string.Join("; ",
                    item.AdditionalDescriptions.Select(kv => $"{kv.Key}: {kv.Value}"));
                row++;
            }
        }

        int lastRow = row - 1;
        // Kwoty: kolumny 7-10, 12
        foreach (int col in new[] { 7, 8, 9, 10, 12 })
            if (lastRow >= 2) ws.Range(2, col, lastRow, col).Style.NumberFormat.Format = MoneyFmt;
        // Ilość: kolumna 6
        if (lastRow >= 2) ws.Range(2, 6, lastRow, 6).Style.NumberFormat.Format = "#,##0.####";

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
    }

    private static void BuildSummarySheet(XLWorkbook wb,
        IReadOnlyList<InvoiceRecord> invoices, FilterCriteria criteria)
    {
        var ws = wb.Worksheets.Add("Podsumowanie");

        ws.Cell("A1").Value = "Raport faktur zakupowych KSeF";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        ws.Cell("A2").Value = "Okres:";
        ws.Cell("B2").Value = $"{criteria.PeriodStart:dd.MM.yyyy} – {criteria.PeriodEnd:dd.MM.yyyy}";
        ws.Cell("A3").Value = "Wygenerowano:";
        ws.Cell("B3").Value = DateTime.Now;
        ws.Cell("B3").Style.DateFormat.Format = DateTimeFmt;
        ws.Cell("A4").Value = "Liczba faktur:";
        ws.Cell("B4").Value = invoices.Count;

        // Flagi
        int splitCount   = invoices.Count(x => x.IsSplitPayment);
        int reverseCount = invoices.Count(x => x.IsReverseCharge);
        int attachCount  = invoices.Count(x => x.HasAttachment);
        ws.Cell("A5").Value = "w tym: podzielona płatność:";   ws.Cell("B5").Value = splitCount;
        ws.Cell("A6").Value = "w tym: odwrotne obciążenie:";   ws.Cell("B6").Value = reverseCount;
        ws.Cell("A7").Value = "w tym: z załącznikiem:";        ws.Cell("B7").Value = attachCount;

        // Typy faktur
        ws.Cell("A9").Value = "Typy faktur:"; ws.Cell("A9").Style.Font.Bold = true;
        int typeRow = 10;
        foreach (var g in invoices.GroupBy(x => InvoiceTypeLabel(x.InvoiceType)).OrderBy(g => g.Key))
        {
            ws.Cell(typeRow, 1).Value = g.Key == "" ? "(nieznany)" : g.Key;
            ws.Cell(typeRow, 2).Value = g.Count();
            typeRow++;
        }

        // Per dostawca
        int tableStart = typeRow + 2;
        ws.Cell(tableStart, 1).Value = "Zestawienie per dostawca";
        ws.Cell(tableStart, 1).Style.Font.Bold = true;
        ws.Cell(tableStart, 1).Style.Font.FontSize = 11;

        string[] headers = ["NIP sprzedawcy", "Nazwa sprzedawcy", "Liczba faktur",
                             "Suma netto", "VAT 23%", "VAT 8%", "VAT 5%", "VAT 0%", "VAT zwol.", "Suma brutto"];
        WriteHeaderRow(ws, tableStart + 1, headers);

        var grouped = invoices.GroupBy(inv => inv.SellerNip, StringComparer.OrdinalIgnoreCase)
                              .OrderBy(g => g.First().SellerName).ToList();

        int row = tableStart + 2;
        foreach (var g in grouped)
        {
            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = g.First().SellerName;
            ws.Cell(row, 3).Value = g.Count();
            ws.Cell(row, 4).Value = g.Sum(x => x.TotalNetValue);
            ws.Cell(row, 5).Value = g.Sum(x => x.VatAmount23);
            ws.Cell(row, 6).Value = g.Sum(x => x.VatAmount8);
            ws.Cell(row, 7).Value = g.Sum(x => x.VatAmount5);
            ws.Cell(row, 8).Value = g.Sum(x => x.VatAmount0);
            ws.Cell(row, 9).Value = g.Sum(x => x.VatAmountExempt);
            ws.Cell(row, 10).Value = g.Sum(x => x.TotalGrossValue);
            row++;
        }

        // Wiersz sumy
        ws.Cell(row, 2).Value = "ŁĄCZNIE";
        ws.Cell(row, 3).Value = invoices.Count;
        ws.Cell(row, 4).Value = invoices.Sum(x => x.TotalNetValue);
        ws.Cell(row, 5).Value = invoices.Sum(x => x.VatAmount23);
        ws.Cell(row, 6).Value = invoices.Sum(x => x.VatAmount8);
        ws.Cell(row, 7).Value = invoices.Sum(x => x.VatAmount5);
        ws.Cell(row, 8).Value = invoices.Sum(x => x.VatAmount0);
        ws.Cell(row, 9).Value = invoices.Sum(x => x.VatAmountExempt);
        ws.Cell(row, 10).Value = invoices.Sum(x => x.TotalGrossValue);
        ws.Range(row, 1, row, 10).Style.Font.Bold = true;
        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = SubHeaderBg;

        var moneyRange = ws.Range(tableStart + 2, 4, row, 10);
        moneyRange.Style.NumberFormat.Format = MoneyFmt;
        moneyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(tableStart + 1);
    }

    // ─────────────────────────────────────────────────────────────── HELPERS ──

    private static void WriteHeaderRow(IXLWorksheet ws, int row, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = HeaderFg;
            cell.Style.Fill.BackgroundColor = HeaderBg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

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
}
