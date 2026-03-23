using FluentAssertions;
using KSeFAssistant.Core.Models;
using Xunit;
using KSeFAssistant.Core.Services;

namespace KSeFAssistant.Tests.Unit;

public sealed class InvoiceFilterServiceTests
{
    private readonly InvoiceFilterService _sut = new();

    private static InvoiceRecord Make(string nip, DateOnly date, decimal gross = 100m) =>
        new()
        {
            KSeFNumber = Guid.NewGuid().ToString(),
            InvoiceNumber = "FV/001",
            IssueDate = date,
            AcquisitionDate = DateTime.UtcNow,
            SellerNip = nip,
            SellerName = $"Firma {nip}",
            TotalGrossValue = gross
        };

    [Fact]
    public void Filter_NoNipCriteria_ReturnsAllInPeriod()
    {
        var invoices = new[]
        {
            Make("1111111111", new DateOnly(2025, 1, 15)),
            Make("2222222222", new DateOnly(2025, 1, 20)),
            Make("3333333333", new DateOnly(2025, 2, 1))  // poza miesiącem
        };
        var criteria = FilterCriteria.ForMonth(2025, 1);

        var result = _sut.Filter(invoices, criteria);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_WithNipCriteria_ReturnsOnlyMatchingNips()
    {
        var invoices = new[]
        {
            Make("1111111111", new DateOnly(2025, 1, 10)),
            Make("2222222222", new DateOnly(2025, 1, 11)),
            Make("3333333333", new DateOnly(2025, 1, 12))
        };
        var criteria = FilterCriteria.ForMonth(2025, 1, ["1111111111", "3333333333"]);

        var result = _sut.Filter(invoices, criteria);

        result.Should().HaveCount(2);
        result.Select(r => r.SellerNip).Should().BeEquivalentTo(["1111111111", "3333333333"]);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsEmpty()
    {
        var result = _sut.Filter([], FilterCriteria.ForMonth(2025, 1));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_DateBoundary_IncludesFirstAndLastDayOfMonth()
    {
        var invoices = new[]
        {
            Make("1111111111", new DateOnly(2025, 3, 1)),   // pierwszy dzień
            Make("1111111111", new DateOnly(2025, 3, 31)),  // ostatni dzień
            Make("1111111111", new DateOnly(2025, 4, 1))    // poza
        };
        var criteria = FilterCriteria.ForMonth(2025, 3);

        var result = _sut.Filter(invoices, criteria);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetUniqueSuppliers_ReturnsDistinctNipsAlphabetically()
    {
        var invoices = new[]
        {
            Make("2222222222", new DateOnly(2025, 1, 1)) with { SellerName = "Beta Sp. z o.o." },
            Make("1111111111", new DateOnly(2025, 1, 2)) with { SellerName = "Alfa S.A." },
            Make("2222222222", new DateOnly(2025, 1, 3)) with { SellerName = "Beta Sp. z o.o." }
        };

        var result = _sut.GetUniqueSuppliers(invoices);

        result.Should().HaveCount(2);
        result[0].Nip.Should().Be("1111111111");  // Alfa alfabetycznie pierwsze
    }

    [Fact]
    public void ForMonth_February2025_CorrectDateRange()
    {
        var criteria = FilterCriteria.ForMonth(2025, 2);

        criteria.PeriodStart.Should().Be(new DateOnly(2025, 2, 1));
        criteria.PeriodEnd.Should().Be(new DateOnly(2025, 2, 28));
    }
}
