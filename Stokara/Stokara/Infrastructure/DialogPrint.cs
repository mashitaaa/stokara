using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using Stokara.Models;
using Stokara.Services;
using Stokara.Views;

namespace Stokara.Infrastructure;

/// <summary>Dialog konfirmasi/isian bertema dan dialog file (FR-30).</summary>
public static class DialogService
{
    private static Window Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

    public static bool Confirm(string message, string title = "Konfirmasi", string okText = "Ya, lanjutkan", bool danger = false)
    {
        var dlg = new DialogWindow(title, message, okText, danger, null, null) { Owner = Owner };
        return dlg.ShowDialog() == true;
    }

    /// <summary>Meminta isian teks (mis. alasan pembatalan). Mengembalikan null bila dibatalkan.</summary>
    public static string Prompt(string message, string title, string label, string initial = "", bool danger = false,
        string okText = "Simpan")
    {
        var dlg = new DialogWindow(title, message, okText, danger, label, initial) { Owner = Owner };
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }

    public static void ShowReceipt(Receipt receipt)
    {
        var w = new ReceiptWindow(receipt) { Owner = Owner };
        w.ShowDialog();
    }

    public static string SaveFile(string filter, string fileName)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = fileName, AddExtension = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public static string OpenFile(string filter)
    {
        var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }
}

/// <summary>Membuat dokumen cetak struk dan laporan (FR-20).</summary>
public static class PrintService
{
    private static readonly FontFamily Mono = new("Consolas");

    public static FlowDocument BuildReceipt(Receipt r)
    {
        var s = AppServices.Settings;
        var doc = new FlowDocument
        {
            PageWidth = 300,
            ColumnWidth = 300,
            PagePadding = new Thickness(12),
            FontFamily = Mono,
            FontSize = 11,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
        Paragraph P(string text, bool bold = false, TextAlignment align = TextAlignment.Left, double size = 11)
        {
            var p = new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0), FontSize = size };
            if (bold) p.FontWeight = FontWeights.Bold;
            doc.Blocks.Add(p);
            return p;
        }
        const string line = "--------------------------------------";

        P(s?.NamaGerai ?? "Gerai Kopdes Merah Putih", true, TextAlignment.Center, 13);
        if (!string.IsNullOrWhiteSpace(s?.AlamatGerai)) P(s.AlamatGerai, false, TextAlignment.Center);
        P(line);
        P(r.Judul, true, TextAlignment.Center);
        P($"No    : {r.NomorDokumen}");
        P($"Tgl   : {Fmt.TglJam(r.Tanggal)}");
        P($"Kasir : {r.Kasir}");
        P($"Untuk : {r.Pihak}");
        P(line);
        foreach (var i in r.Items)
        {
            P(i.BarangNama + (string.IsNullOrEmpty(i.Keterangan) ? "" : $" [{i.Keterangan}]"));
            var left = $"  {Fmt.Num(i.Jumlah)} {i.Satuan} x {i.Harga.ToString("N0", Fmt.Id)}";
            var right = i.Subtotal.ToString("N0", Fmt.Id);
            P(left.PadRight(Math.Max(left.Length + 1, 38 - right.Length)) + right);
        }
        P(line);
        string Row(string label, decimal v) => label.PadRight(20) + v.ToString("N0", Fmt.Id).PadLeft(18);
        P(Row("TOTAL", r.Total), true);
        if (r.UangDiterima > 0)
        {
            P(Row("Tunai", r.UangDiterima));
            P(Row("Kembali", r.Kembalian));
        }
        if (!string.IsNullOrEmpty(r.Catatan)) P(r.Catatan, false, TextAlignment.Center);
        P(line);
        P("Terima kasih. Simpan struk ini.", false, TextAlignment.Center);
        P("Dicetak oleh STOKARA", false, TextAlignment.Center, 9);
        return doc;
    }

    public static FlowDocument BuildReport(ReportResult report)
    {
        var doc = new FlowDocument
        {
            PageWidth = 1056,     // A4 lanskap pada 96 dpi
            PageHeight = 816,
            ColumnWidth = 1056,
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
        doc.Blocks.Add(new Paragraph(new Run(AppServices.Settings?.NamaGerai)) { FontSize = 12, Margin = new Thickness(0) });
        doc.Blocks.Add(new Paragraph(new Bold(new Run(report.Judul))) { FontSize = 18, Margin = new Thickness(0, 4, 0, 0) });
        doc.Blocks.Add(new Paragraph(new Run(report.Subjudul + " · dicetak " + Fmt.TglJam(DateTime.Now)))
            { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 10) });
        if (report.Ringkasan.Count > 0)
            doc.Blocks.Add(new Paragraph(new Run(string.Join("   ·   ", report.Ringkasan.Select(k => $"{k.Key}: {k.Value}"))))
                { FontWeight = FontWeights.SemiBold });

        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
        foreach (DataColumn _ in report.Table.Columns) table.Columns.Add(new TableColumn());
        var group = new TableRowGroup();
        var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xE9, 0xD8)) };
        foreach (DataColumn c in report.Table.Columns)
            header.Cells.Add(Cell(c.ColumnName, true, false));
        group.Rows.Add(header);
        foreach (DataRow row in report.Table.Rows)
        {
            var tr = new TableRow();
            foreach (DataColumn c in report.Table.Columns)
                tr.Cells.Add(Cell(FormatCell(row[c]), false, row[c] is decimal or double or int or long));
            group.Rows.Add(tr);
        }
        table.RowGroups.Add(group);
        doc.Blocks.Add(table);
        return doc;
    }

    private static TableCell Cell(string text, bool bold, bool right)
    {
        var p = new Paragraph(new Run(text)) { Margin = new Thickness(0), TextAlignment = right ? TextAlignment.Right : TextAlignment.Left };
        if (bold) p.FontWeight = FontWeights.SemiBold;
        return new TableCell(p) { Padding = new Thickness(4, 2, 4, 2), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) };
    }

    public static string FormatCell(object v) => v switch
    {
        null or DBNull => "",
        decimal d => d.ToString("#,0.##", Fmt.Id),
        double d => d.ToString("#,0.##", Fmt.Id),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy") : dt.ToString("dd/MM/yyyy HH:mm"),
        _ => v.ToString()
    };

    public static void Print(FlowDocument doc, string description)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;
        IDocumentPaginatorSource source = doc;
        dlg.PrintDocument(source.DocumentPaginator, description);
    }
}
