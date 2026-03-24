namespace KSeFAssistant.Core.Models;

/// <summary>
/// Domenowy model faktury zakupowej pobranej z KSeF.
/// Mapowany z metadanych API (zawsze) + XML FA_v3 (po LoadInvoiceXmlAsync).
/// </summary>
public sealed record InvoiceRecord
{
    // --- Identyfikatory ---
    public string KSeFNumber { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string InvoiceType { get; init; } = string.Empty;   // Vat | Kor | Zal | Roz | Upr | KorZal | KorRoz
    public string? InvoiceHash { get; init; }                  // SHA-256 z metadanych KSeF

    // --- Daty ---
    public DateOnly IssueDate { get; init; }                   // data wystawienia (P_1)
    public DateOnly? SaleDate { get; init; }                   // data sprzedaży (P_6)
    public DateTime AcquisitionDate { get; init; }             // data nadania numeru KSeF
    public DateTime InvoicingDate { get; init; }               // data przyjęcia w KSeF
    public DateTime PermanentStorageDate { get; init; }        // data trwałego zapisu

    // --- Sprzedawca ---
    public string SellerNip { get; init; } = string.Empty;
    public string SellerName { get; init; } = string.Empty;
    public string SellerStreet { get; init; } = string.Empty;
    public string SellerCity { get; init; } = string.Empty;
    public string SellerPostCode { get; init; } = string.Empty;
    public string SellerCountry { get; init; } = string.Empty; // KodKraju → nazwa (np. Polska)

    // --- Nabywca ---
    public string BuyerNip { get; init; } = string.Empty;     // NIP lub inny identyfikator
    public string BuyerName { get; init; } = string.Empty;
    public string BuyerStreet { get; init; } = string.Empty;
    public string BuyerCity { get; init; } = string.Empty;
    public string BuyerPostCode { get; init; } = string.Empty;
    public string BuyerCountry { get; init; } = string.Empty;  // KodKraju → nazwa

    // --- Wartości ---
    public decimal TotalNetValue { get; init; }
    public decimal TotalVatValue { get; init; }
    public decimal VatAmount23 { get; init; }
    public decimal VatAmount8 { get; init; }
    public decimal VatAmount5 { get; init; }
    public decimal VatAmount0 { get; init; }
    public decimal VatAmountExempt { get; init; }
    public decimal TotalGrossValue { get; init; }

    // --- Waluta i płatność ---
    public string Currency { get; init; } = "PLN";
    public string PaymentMethod { get; init; } = string.Empty;         // FormaPlatnosci (1=gotówka, 6=przelew…)
    public DateOnly? PaymentDueDate { get; init; }                      // pierwszy termin płatności
    public bool IsPaid { get; init; }                                   // Zaplacono=1
    public DateOnly? PaymentDate { get; init; }                         // DataZaplaty
    public IReadOnlyList<string> BankAccountNumbers { get; init; } = []; // rachunki bankowe sprzedawcy

    // --- Adnotacje (flagi z sekcji Adnotacje FA_v3) ---
    public bool IsReverseCharge { get; init; }   // P_16 — odwrotne obciążenie
    public bool IsSplitPayment { get; init; }     // P_18 — mechanizm podzielonej płatności
    public bool IsCashAccounting { get; init; }   // P_18A — metoda kasowa
    public bool IsSelfInvoicing { get; init; }    // P_17 / metadane — samofakturowanie
    public bool HasAttachment { get; init; }      // metadane — czy ma załącznik

    // --- Miejsce wystawienia i informacje dodatkowe ---
    public string PlaceOfIssue { get; init; } = string.Empty;           // P_1M
    public string ContractNumber { get; init; } = string.Empty;         // WarunkiTransakcji/Umowy/NrUmowy
    public IReadOnlyList<KeyValuePair<string, string>> AdditionalNotes { get; init; } = []; // DodatkowyOpis

    // --- Pozycje faktury (ładowane na żądanie z XML) ---
    public IReadOnlyList<InvoiceLineItem> LineItems { get; init; } = [];

    // --- Status pobrania ---
    public bool XmlLoaded { get; set; }
    public string? ParseError { get; set; }
}
