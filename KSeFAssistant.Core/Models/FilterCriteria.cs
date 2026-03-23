namespace KSeFAssistant.Core.Models;

public sealed class FilterCriteria
{
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    /// <summary>
    /// Lista NIP dostawców do filtrowania. Pusta = pobierz wszystkie.
    /// </summary>
    public IReadOnlySet<string> SupplierNips { get; init; } = new HashSet<string>();

    /// <summary>
    /// Tworzenie kryterium dla pełnego miesiąca.
    /// </summary>
    public static FilterCriteria ForMonth(int year, int month, IEnumerable<string>? supplierNips = null)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return new FilterCriteria
        {
            PeriodStart = start,
            PeriodEnd = end,
            SupplierNips = supplierNips != null
                ? new HashSet<string>(supplierNips, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>()
        };
    }
}
