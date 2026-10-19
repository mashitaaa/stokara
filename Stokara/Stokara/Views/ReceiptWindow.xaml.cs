using System.Windows;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Views;

/// <summary>Pratinjau dan cetak struk/bukti penyaluran (FR-20).</summary>
public partial class ReceiptWindow : Window
{
    private readonly Receipt _receipt;

    public ReceiptWindow(Receipt receipt)
    {
        InitializeComponent();
        _receipt = receipt;
        Heading.Text = $"{receipt.Judul} · {receipt.NomorDokumen}";
        Viewer.Document = PrintService.BuildReceipt(receipt);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Dokumen baru untuk dicetak agar tata letak pratinjau tidak terganggu.
            PrintService.Print(PrintService.BuildReceipt(_receipt), _receipt.NomorDokumen);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex);
        }
    }
}
