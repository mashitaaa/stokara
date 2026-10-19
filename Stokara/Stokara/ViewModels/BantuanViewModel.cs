using Stokara.Helpers;

namespace Stokara.ViewModels;

public sealed class PanduanBagian
{
    public string Judul { get; init; }
    public string Ikon { get; init; }
    public string[] Langkah { get; init; }
}

/// <summary>Panduan singkat untuk alur kasir, penerimaan, dan penyaluran subsidi (FR-33, PRD 10.1).</summary>
public sealed class BantuanViewModel : ViewModelBase
{
    public List<PanduanBagian> Bagian { get; } = new()
    {
        new()
        {
            Judul = "Melayani penjualan", Ikon = "",
            Langkah = new[]
            {
                "Tekan F2 untuk membuka Kasir. Pastikan mode \"Penjualan\" terpilih.",
                "Pilih pelanggan Umum, atau Anggota lalu cari nama/nomor anggota. Harga anggota terpasang otomatis.",
                "Scan barcode atau ketik nama barang lalu tekan Enter. Ulangi untuk barang lain.",
                "Ubah jumlah dengan tombol + dan −. Jumlah tidak bisa melebihi stok.",
                "Isi uang diterima (atau tekan Uang Pas), lalu tekan Bayar / Ctrl+S. Struk muncul dan bisa dicetak."
            }
        },
        new()
        {
            Judul = "Menyalurkan barang subsidi", Ikon = "",
            Langkah = new[]
            {
                "Di Kasir, pilih mode \"Penyaluran Subsidi\".",
                "Cari penerima dengan NIK atau nama. Cocokkan dengan kartu identitas warga.",
                "Periksa sisa kuota yang tampil di panel kanan.",
                "Scan barang subsidi. Sistem menolak bila penerima tidak aktif, harga di atas HET, kuota habis, atau stok kurang.",
                "Tekan Bayar. Bukti penyaluran dapat dicetak dan masuk ke Laporan Penyaluran Subsidi."
            }
        },
        new()
        {
            Judul = "Menerima barang datang", Ikon = "",
            Langkah = new[]
            {
                "Buka Stok Masuk & Keluar, tab Penerimaan, lalu tekan Penerimaan Baru (Ctrl+N).",
                "Pilih sumber: Pembelian, Pasokan Subsidi, Konsinyasi (titipan), atau Stok Awal.",
                "Pilih pemasok atau penitip dan isi nomor surat jalan.",
                "Cari barang, isi jumlah, harga, dan tanggal kedaluwarsa bila ada, lalu Tambah.",
                "Tekan Simpan. Stok bertambah dan tercatat di kartu stok."
            }
        },
        new()
        {
            Judul = "Barang titipan (konsinyasi)", Ikon = "",
            Langkah = new[]
            {
                "Manajer mendaftarkan penitip (menu Mitra) dan barang titipan dengan harga titip (menu Barang).",
                "Barang diterima lewat Penerimaan sumber Konsinyasi.",
                "Saat barang titipan terjual, hutang ke penitip tercatat otomatis.",
                "Bendahara membuka Konsinyasi, memilih penitip, lalu mencatat pembayaran. Pembayaran melunasi hutang tertua lebih dulu."
            }
        },
        new()
        {
            Judul = "Stok opname", Ikon = "",
            Langkah = new[]
            {
                "Petugas gudang membuat sesi opname (semua atau per kategori).",
                "Hitung barang di rak lalu isi kolom Stok Fisik. Isi alasan untuk setiap selisih.",
                "Tekan Ajukan. Manajer atau Pengurus lain memeriksa lalu menyetujui atau menolak.",
                "Setelah disetujui, stok menyesuaikan dan tercatat sebagai mutasi Penyesuaian."
            }
        },
        new()
        {
            Judul = "Akhir hari", Ikon = "",
            Langkah = new[]
            {
                "Buka Riwayat Transaksi, tab Tutup Kasir.",
                "Isi modal awal dan uang fisik di laci. Sistem menghitung selisih.",
                "Tekan Tutup Hari. Setelah ditutup, transaksi hari itu tidak bisa ditambah atau dibatalkan."
            }
        }
    };

    public List<KeyValuePair<string, string>> Pintasan { get; } = new()
    {
        new("F2", "Buka Kasir"),
        new("Ctrl + N", "Data/transaksi baru di halaman aktif"),
        new("Ctrl + S", "Simpan / Bayar"),
        new("Ctrl + F", "Pindah ke kolom pencarian"),
        new("Esc", "Batal / tutup saran"),
        new("Enter", "Tambahkan barang hasil scan di kasir")
    };
}
