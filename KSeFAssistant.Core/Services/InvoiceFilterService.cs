using KSeFAssistant.Core.Models;

namespace KSeFAssistant.Core.Services;

public sealed class InvoiceFilterService
{
    /// <summary>
    /// Filtruje faktury po kryteriach: zakres dat + lista NIP dostawców.
    /// Filtrowanie odbywa się po stronie klienta (KSeF API nie filtruje po NIP sprzedawcy).
    /// </summary>
    public IReadOnlyList<InvoiceRecord> Filter(
        IEnumerable<InvoiceRecord> invoices, FilterCriteria criteria)
    {
        var result = invoices
            .Where(inv => inv.IssueDate >= criteria.PeriodStart
                       && inv.IssueDate <= criteria.PeriodEnd);

        if (criteria.SupplierNips.Count > 0)
        {
            result = result.Where(inv =>
                criteria.SupplierNips.Contains(inv.SellerNip));
        }

        return result.ToList();
    }

    /// <summary>
    /// Zwraca unikalną listę NIP dostawców z kolekcji faktur (do multi-select).
    /// </summary>
    public IReadOnlyList<(string Nip, string Name)> GetUniqueSuppliers(
        IEnumerable<InvoiceRecord> invoices)
    {
        return invoices
            .GroupBy(inv => inv.SellerNip, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Nip: g.Key, Name: g.First().SellerName))
            .OrderBy(x => x.Name)
            .ToList();
    }
}
