namespace KSeFAssistant.Core.Models;

/// <summary>
/// Domenowy model faktury zakupowej pobranej z KSeF.
/// Mapowany z XML FA_v3 przez KSeFDtoMapper.
/// </summary>
public sealed record InvoiceRecord
{
    // --- Identyfikatory ---
    public string KSeFNumber { get; init; } = string.Empty;       // Numer KSeF (ksefReferenceNumber)
    public string InvoiceNumber { get; init; } = string.Empty;    // /Faktura/Fa/P_2

    // --- Daty ---
    public DateOnly IssueDate { get; init; }       // /Faktura/Fa/P_1  (data wystawienia)
    public DateOnly? SaleDate { get; init; }       // /Faktura/Fa/P_6  (data sprzedaży)
    public DateTime AcquisitionDate { get; init; } // data przyjęcia przez KSeF

    // --- Sprzedawca ---
    public string SellerNip { get; init; } = string.Empty;
    public string SellerName { get; init; } = string.Empty;
    public string SellerStreet { get; init; } = string.Empty;
    public string SellerCity { get; init; } = string.Empty;
    public string SellerPostCode { get; init; } = string.Empty;

    // --- Nabywca ---
    public string BuyerNip { get; init; } = string.Empty;
    public string BuyerName { get; init; } = string.Empty;
    public string BuyerStreet { get; init; } = string.Empty;
    public string BuyerCity { get; init; } = string.Empty;
    public string BuyerPostCode { get; init; } = string.Empty;

    // --- Wartości (per stawka VAT) ---
    public decimal TotalNetValue { get; init; }       // /Faktura/Fa/P_15
    public decimal VatAmount23 { get; init; }          // /Faktura/Fa/P_14_1
    public decimal VatAmount8 { get; init; }           // /Faktura/Fa/P_14_2
    public decimal VatAmount5 { get; init; }           // /Faktura/Fa/P_14_3
    public decimal VatAmount0 { get; init; }           // stawka 0%
    public decimal VatAmountExempt { get; init; }      // zwolnione
    public decimal TotalGrossValue { get; init; }      // suma brutto

    // --- Waluta i płatność ---
    public string Currency { get; init; } = "PLN";    // /Faktura/Fa/KodWaluty
    public string PaymentMethod { get; init; } = string.Empty;
    public DateOnly? PaymentDueDate { get; init; }

    // --- Pozycje faktury (ładowane na żądanie) ---
    public IReadOnlyList<InvoiceLineItem> LineItems { get; init; } = [];

    // --- Status pobrania ---
    public bool XmlLoaded { get; set; }
    public string? ParseError { get; set; }
}
