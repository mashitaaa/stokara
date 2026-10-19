using Stokara.Helpers;
using Stokara.Infrastructure;

namespace Stokara.Models;

public static class Roles
{
    public const string Manajer = "MANAJER";
    public const string Kasir = "KASIR";
    public const string Gudang = "GUDANG";
    public const string Pengurus = "PENGURUS";

    public static readonly string[] All = { Manajer, Kasir, Gudang, Pengurus };

    public static string Label(string role) => role switch
    {
        Manajer => "Manajer Gerai",
        Kasir => "Kasir",
        Gudang => "Petugas Gudang",
        Pengurus => "Pengurus",
        _ => role
    };
}

public sealed class User
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string Nama { get; set; }
    public string Role { get; set; }
    public string Tema { get; set; } = "Light";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string RoleLabel => Roles.Label(Role);
    public string StatusLabel => IsActive ? "AKTIF" : "NONAKTIF";
    public string Inisial => string.IsNullOrWhiteSpace(Nama) ? "?" :
        string.Concat(Nama.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpper(s[0])));
}

/// <summary>Data sederhana id-nama untuk kategori, satuan, dan ComboBox.</summary>
public sealed class LookupItem
{
    public int Id { get; set; }
    public string Nama { get; set; }
    public bool IsActive { get; set; } = true;
    public string StatusLabel => IsActive ? "AKTIF" : "NONAKTIF";
    public override string ToString() => Nama;
}

public sealed class Pemasok
{
    public int PemasokId { get; set; }
    public string Kode { get; set; }
    public string Nama { get; set; }
    public string Jenis { get; set; } = "LOKAL";
    public string Telepon { get; set; }
    public string Alamat { get; set; }
    public bool IsActive { get; set; } = true;
    public string StatusLabel => IsActive ? "AKTIF" : "NONAKTIF";
    public override string ToString() => $"{Nama} ({Kode})";
}

public sealed class Penitip
{
    public int PenitipId { get; set; }
    public string Nama { get; set; }
    public string Telepon { get; set; }
    public string Alamat { get; set; }
    public bool IsActive { get; set; } = true;
    public string StatusLabel => IsActive ? "AKTIF" : "NONAKTIF";
    public override string ToString() => Nama;
}

public sealed class Anggota
{
    public int AnggotaId { get; set; }
    public string NomorAnggota { get; set; }
    public string NIK { get; set; }
    public string Nama { get; set; }
    public string Alamat { get; set; }
    public string Dusun { get; set; }
    public string Telepon { get; set; }
    public string Status { get; set; } = "AKTIF";
    public DateTime TanggalBergabung { get; set; } = DateTime.Today;
    public string NikTampil { get; set; }
    public override string ToString() => $"{Nama} ({NomorAnggota})";
}

public sealed class PenerimaSubsidi
{
    public int PenerimaId { get; set; }
    public string NIK { get; set; }
    public string Nama { get; set; }
    public string JenisPenerima { get; set; } = "RUMAH_TANGGA";
    public string Dusun { get; set; }
    public int? AnggotaId { get; set; }
    public string Status { get; set; } = "AKTIF";
    public string NikTampil { get; set; }
    public string JenisLabel => Fmt.JenisPenerimaLabel(JenisPenerima);
    public override string ToString() => $"{Nama} ({NikTampil})";
}

public sealed class Barang
{
    public int BarangId { get; set; }
    public string SKU { get; set; }
    public string Barcode { get; set; }
    public string Nama { get; set; }
    public int KategoriId { get; set; }
    public string KategoriNama { get; set; }
    public int SatuanId { get; set; }
    public string SatuanNama { get; set; }
    public string Kepemilikan { get; set; } = "MILIK";
    public int? PenitipId { get; set; }
    public string PenitipNama { get; set; }
    public decimal? HargaTitip { get; set; }
    public decimal HargaAnggota { get; set; }
    public decimal HargaUmum { get; set; }
    public decimal HargaPokokRata { get; set; }
    public bool IsSubsidi { get; set; }
    public decimal? HET { get; set; }
    public decimal StokMinimum { get; set; }
    public decimal StokSaatIni { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsTitipan => Kepemilikan == "TITIPAN";

    /// <summary>BR-04: Habis bila 0, Menipis bila 0 &lt; stok ≤ minimum, selain itu Aman.</summary>
    public string StatusStok => StokSaatIni <= 0 ? "Habis" : StokSaatIni <= StokMinimum ? "Menipis" : "Aman";

    public string JenisLabel => IsSubsidi ? "SUBSIDI" : IsTitipan ? "TITIPAN" : "BEBAS";
    public string StatusLabel => IsActive ? "AKTIF" : "NONAKTIF";
    public decimal NilaiPersediaan => IsTitipan ? 0 : StokSaatIni * HargaPokokRata;
    public decimal HargaUntuk(bool anggota) => anggota ? HargaAnggota : HargaUmum;
    public override string ToString() => $"{Nama} ({SKU})";
}

public sealed class AturanKuota
{
    public int AturanId { get; set; }
    public int BarangId { get; set; }
    public string BarangNama { get; set; }
    public string SatuanNama { get; set; }
    public string JenisPenerima { get; set; }
    public string Periode { get; set; }
    public decimal JumlahKuota { get; set; }
    public int JumlahPenerima { get; set; }
    public decimal TotalTerpakai { get; set; }
    public string JenisLabel => Fmt.JenisPenerimaLabel(JenisPenerima);
}

/// <summary>Sisa kuota satu penerima untuk satu barang subsidi pada periode berjalan.</summary>
public sealed class SisaKuota
{
    public int BarangId { get; set; }
    public string BarangNama { get; set; }
    public string SatuanNama { get; set; }
    public decimal Kuota { get; set; }
    public decimal Terpakai { get; set; }
    public decimal Sisa => Math.Max(0, Kuota - Terpakai);
    public double PersenTerpakai => Kuota <= 0 ? 0 : (double)Math.Min(1m, Terpakai / Kuota) * 100.0;
    public string Ringkas => $"{Fmt.Num(Sisa)} dari {Fmt.Num(Kuota)} {SatuanNama}";
}

public sealed class MutasiRow
{
    public DateTime Tanggal { get; set; }
    public string JenisMutasi { get; set; }
    public string NomorDokumen { get; set; }
    public decimal Masuk { get; set; }
    public decimal Keluar { get; set; }
    public decimal SaldoSetelah { get; set; }
    public string Keterangan { get; set; }
    public string Pengguna { get; set; }
    public string JenisLabel => Fmt.MutasiLabel(JenisMutasi);
}

public sealed class Aktivitas
{
    public long AktivitasId { get; set; }
    public string Pengguna { get; set; }
    public string JenisAksi { get; set; }
    public string Entitas { get; set; }
    public string EntitasId { get; set; }
    public string Keterangan { get; set; }
    public DateTime Waktu { get; set; }
}

public sealed class ChartPoint
{
    public string Label { get; set; }
    public double Value { get; set; }
    /// <summary>Nilai pembanding (mis. total kuota) untuk grafik progres.</summary>
    public double Value2 { get; set; }
    public ChartPoint() { }
    public ChartPoint(string label, double value, double value2 = 0) { Label = label; Value = value; Value2 = value2; }
}

/// <summary>Satu baris keranjang kasir atau baris item form penerimaan/pengeluaran.</summary>
public sealed class LineItem : ObservableObject
{
    private decimal _jumlah;
    private decimal _harga;
    private DateTime? _kedaluwarsa;

    public Barang Barang { get; init; }
    public int BarangId => Barang.BarangId;
    public string Nama => Barang.Nama;
    public string Satuan => Barang.SatuanNama;
    public string Jenis => Barang.JenisLabel;

    public decimal Jumlah
    {
        get => _jumlah;
        set { if (SetProperty(ref _jumlah, value)) OnPropertyChanged(nameof(Subtotal)); }
    }

    public decimal Harga
    {
        get => _harga;
        set { if (SetProperty(ref _harga, value)) OnPropertyChanged(nameof(Subtotal)); }
    }

    public DateTime? TanggalKedaluwarsa
    {
        get => _kedaluwarsa;
        set => SetProperty(ref _kedaluwarsa, value);
    }

    public decimal Subtotal => Jumlah * Harga;
}

public sealed class TransaksiRow
{
    public int Id { get; set; }
    public string NomorDokumen { get; set; }
    public DateTime Tanggal { get; set; }
    public string Jenis { get; set; }
    public string Pihak { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
    public string Pengguna { get; set; }
    public int UserId { get; set; }
    public string AlasanBatal { get; set; }
    public string JenisLabel => Jenis switch
    {
        "PENJUALAN" => "Penjualan",
        "PENYALURAN_SUBSIDI" => "Penyaluran Subsidi",
        _ => Fmt.SumberLabel(Jenis)
    };
}

public sealed class TransaksiDetailRow
{
    public string BarangNama { get; set; }
    public string Satuan { get; set; }
    public decimal Jumlah { get; set; }
    public decimal Harga { get; set; }
    public decimal Subtotal { get; set; }
    public string Keterangan { get; set; }
}

public sealed class HutangRow
{
    public int HutangId { get; set; }
    public DateTime Tanggal { get; set; }
    public string NomorDokumen { get; set; }
    public string BarangNama { get; set; }
    public decimal Jumlah { get; set; }
    public decimal HargaTitip { get; set; }
    public decimal NilaiHutang { get; set; }
    public decimal Komisi { get; set; }
    public decimal JumlahDibayar { get; set; }
    public decimal Sisa => NilaiHutang - JumlahDibayar;
    public string Status { get; set; }
}

public sealed class PenitipRingkas
{
    public int PenitipId { get; set; }
    public string Nama { get; set; }
    public decimal TotalHutang { get; set; }
    public decimal TotalDibayar { get; set; }
    public decimal Sisa => TotalHutang - TotalDibayar;
    public decimal TotalKomisi { get; set; }
    public decimal StokTitipanNilai { get; set; }
}

public sealed class PelunasanRow
{
    public string NomorDokumen { get; set; }
    public DateTime Tanggal { get; set; }
    public string PenitipNama { get; set; }
    public decimal JumlahBayar { get; set; }
    public string Catatan { get; set; }
    public string Pengguna { get; set; }
}

public sealed class OpnameRow
{
    public int OpnameId { get; set; }
    public string NomorDokumen { get; set; }
    public DateTime Tanggal { get; set; }
    public string Kategori { get; set; }
    public string Status { get; set; }
    public string DibuatOleh { get; set; }
    public int? DiajukanOlehId { get; set; }
    public string DiajukanOleh { get; set; }
    public string DisetujuiOleh { get; set; }
    public int JumlahBarang { get; set; }
    public int JumlahSelisih { get; set; }
    public string Catatan { get; set; }
    public string AlasanTolak { get; set; }
}

public sealed class OpnameDetailRow : ObservableObject
{
    private decimal? _stokFisik;
    private string _alasan;

    public int DetailId { get; set; }
    public int BarangId { get; set; }
    public string SKU { get; set; }
    public string BarangNama { get; set; }
    public string Satuan { get; set; }
    public decimal StokSistem { get; set; }

    public decimal? StokFisik
    {
        get => _stokFisik;
        set
        {
            if (SetProperty(ref _stokFisik, value))
            {
                OnPropertyChanged(nameof(Selisih));
                OnPropertyChanged(nameof(SelisihLabel));
            }
        }
    }

    public string Alasan
    {
        get => _alasan;
        set => SetProperty(ref _alasan, value);
    }

    public decimal? Selisih => StokFisik.HasValue ? StokFisik.Value - StokSistem : null;

    public string SelisihLabel => Selisih switch
    {
        null => "-",
        0m => "Sesuai",
        > 0m => "+" + Fmt.Num(Selisih.Value),
        _ => Fmt.Num(Selisih.Value)
    };
}

public sealed class TutupHariRow
{
    public DateTime Tanggal { get; set; }
    public int JumlahTransaksi { get; set; }
    public decimal TotalPenjualan { get; set; }
    public decimal ModalAwal { get; set; }
    public decimal UangSeharusnya { get; set; }
    public decimal UangFisik { get; set; }
    public decimal Selisih { get; set; }
    public string Catatan { get; set; }
    public string Pengguna { get; set; }
}

public sealed class DashboardStats
{
    public decimal PenjualanHariIni { get; set; }
    public int TransaksiHariIni { get; set; }
    public decimal NilaiPersediaan { get; set; }
    public int StokKritis { get; set; }
    public decimal HutangPenitip { get; set; }
    public decimal SubsidiTersalur { get; set; }
    public decimal SubsidiKuota { get; set; }
    public int PerkiraanKedaluwarsa { get; set; }
    public int JumlahBarang { get; set; }
}

public sealed class PeringatanRow
{
    public int BarangId { get; set; }
    public string Nama { get; set; }
    public string Satuan { get; set; }
    public decimal Stok { get; set; }
    public decimal StokMinimum { get; set; }
    public DateTime? Kedaluwarsa { get; set; }
    public string Status { get; set; }
    public string Keterangan => Kedaluwarsa.HasValue
        ? $"Perkiraan kedaluwarsa {Fmt.Tgl(Kedaluwarsa.Value)}"
        : $"Stok {Fmt.Num(Stok)} {Satuan} (min. {Fmt.Num(StokMinimum)})";
}

/// <summary>Data struk/bukti transaksi untuk dicetak.</summary>
public sealed class Receipt
{
    public string Judul { get; set; }
    public string NomorDokumen { get; set; }
    public DateTime Tanggal { get; set; }
    public string Kasir { get; set; }
    public string Pihak { get; set; }
    public List<TransaksiDetailRow> Items { get; set; } = new();
    public decimal Total { get; set; }
    public decimal UangDiterima { get; set; }
    public decimal Kembalian { get; set; }
    public string Catatan { get; set; }
}
