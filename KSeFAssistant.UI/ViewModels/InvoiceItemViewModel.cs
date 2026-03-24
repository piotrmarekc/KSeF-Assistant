using CommunityToolkit.Mvvm.ComponentModel;
using KSeFAssistant.Core.Models;

namespace KSeFAssistant.UI.ViewModels;

/// <summary>
/// Wrapper InvoiceRecord z właściwością IsSelected do DataGrid.
/// </summary>
public sealed partial class InvoiceItemViewModel : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public InvoiceRecord Invoice { get; }

    // Właściwości wyświetlane w DataGrid
    public string KSeFNumber => Invoice.KSeFNumber;
    public string InvoiceNumber => Invoice.InvoiceNumber;
    public string IssueDate => Invoice.IssueDate.ToString("dd.MM.yyyy");
    public string SellerNip => Invoice.SellerNip;
    public string SellerName => Invoice.SellerName;
    public decimal TotalNetValue => Invoice.TotalNetValue;
    public decimal TotalGrossValue => Invoice.TotalGrossValue;
    public decimal VatTotal => Invoice.TotalVatValue;
    public string Currency => Invoice.Currency;
    public string StatusIcon => Invoice.ParseError is not null ? "⚠" : Invoice.XmlLoaded ? "✓" : "·";

    public InvoiceItemViewModel(InvoiceRecord invoice)
    {
        Invoice = invoice;
    }
}
