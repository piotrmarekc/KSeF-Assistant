namespace KSeFAssistant.Core.Models;

/// <summary>
/// Pozycja na fakturze (mapowana z elementu FaWiersz w FA_v3).
/// </summary>
public sealed class InvoiceLineItem
{
    public int LineNumber { get; init; }
    public string UuId { get; init; } = string.Empty;         // UU_ID — unikalny numer wiersza wystawcy
    public string Name { get; init; } = string.Empty;         // P_7
    public string Unit { get; init; } = string.Empty;         // P_8A
    public decimal Quantity { get; init; }                     // P_8B
    public decimal UnitPriceNet { get; init; }                 // P_9A
    public decimal UnitPriceGross { get; init; }               // P_9B
    public decimal DiscountAmount { get; init; }               // P_10
    public decimal NetValue { get; init; }                     // P_11
    public decimal GrossValue { get; init; }                   // P_11A
    public decimal VatAmountLine { get; init; }                // P_11Vat — kwota VAT pozycji
    public string VatRate { get; init; } = string.Empty;      // P_12: "23", "8", "5", "0", "zw", "np"
    public decimal VatAmount { get; init; }                    // P_14
    public string GtuCode { get; init; } = string.Empty;      // GTU_01..GTU_13
    public string Gtin { get; init; } = string.Empty;         // GTIN — globalny numer towaru
    public string CnCode { get; init; } = string.Empty;       // CN — kod CN/HS
    public string ProductIndex { get; init; } = string.Empty; // Indeks — indeks własny wystawcy
    public IReadOnlyList<KeyValuePair<string, string>> AdditionalDescriptions { get; init; } = [];
}
