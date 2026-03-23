namespace KSeFAssistant.Core.Models;

/// <summary>
/// Pozycja na fakturze (mapowana z elementu FaWiersz w FA_v3).
/// </summary>
public sealed class InvoiceLineItem
{
    public int LineNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPriceNet { get; init; }
    public decimal NetValue { get; init; }
    public decimal GrossValue { get; init; }
    public string VatRate { get; init; } = string.Empty;   // "23", "8", "5", "0", "zw", "np"
    public decimal VatAmount { get; init; }
}
