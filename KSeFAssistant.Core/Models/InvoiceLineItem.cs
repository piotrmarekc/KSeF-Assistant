namespace KSeFAssistant.Core.Models;

/// <summary>
/// Pozycja na fakturze (mapowana z elementu FaWiersz w FA_v3).
/// </summary>
public sealed class InvoiceLineItem
{
    public int LineNumber { get; init; }
    public string Name { get; init; } = string.Empty;        // P_7
    public string Unit { get; init; } = string.Empty;         // P_8A
    public decimal Quantity { get; init; }                     // P_8B
    public decimal UnitPriceNet { get; init; }                 // P_9A
    public decimal DiscountAmount { get; init; }               // P_10 (rabat)
    public decimal NetValue { get; init; }                     // P_11
    public decimal GrossValue { get; init; }                   // P_11A
    public string VatRate { get; init; } = string.Empty;      // P_12: "23", "8", "5", "0", "zw", "np"
    public decimal VatAmount { get; init; }                    // P_14
    public string GtuCode { get; init; } = string.Empty;      // GTU_01..GTU_13
    public IReadOnlyList<KeyValuePair<string, string>> AdditionalDescriptions { get; init; } = [];
}
