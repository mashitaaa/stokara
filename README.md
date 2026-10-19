# STOKARA - Inventori & Penyaluran Gerai Kopdes Merah Putih

Aplikasi desktop **WPF (.NET 10) + SQL Server** dengan pola **MVVM**.
Berjalan offline di satu komputer gerai.

---

## 1. Kebutuhan

| Komponen | Versi |
| --- | --- |
| Windows | 10 / 11 |
| Visual Studio 2022 (17.8 atau lebih baru), workload **.NET desktop development** | - |
| .NET SDK | 10.0 |
| SQL Server Express (instance `SQLEXPRESS`) | 2019 / 2022 |
| SQL Server Management Studio (opsional, untuk melihat tabel) | - |

Satu-satunya paket NuGet: `Microsoft.Data.SqlClient`. Grafik, kartu statistik, toast, dan dialog dibuat sendiri (tanpa pustaka pihak ketiga).

## 2. Menjalankan pertama kali

1. Pasang **SQL Server Express** (pilih *Basic*). Pastikan layanan **SQL Server (SQLEXPRESS)** berstatus *Running* (`services.msc`).
2. Buka `Stokara.sln` di Visual Studio, tunggu *restore* NuGet selesai.
3. Bila instance SQL Server Anda bukan `.\SQLEXPRESS`, ubah `Stokara/appsettings.json`:
   ```json
   "Stokara": "Server=.\\SQLEXPRESS;Database=STOKARA_DB;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5"
   ```
   Contoh lain: `Server=(localdb)\\MSSQLLocalDB;...` atau `Server=localhost;...`.
4. Tekan **F5**. Saat pertama dibuka, aplikasi otomatis:
   - membuat database `STOKARA_DB`,
   - menjalankan `Database/schema.sql` dan `Database/seed.sql`,
   - membuat empat akun awal,
   - membuat **data contoh** (stok awal, penjualan 7 hari, penyaluran subsidi, hutang penitip) lewat service yang sama dengan aplikasi, supaya mutasi, HPP, kuota, dan hutang konsisten.
     Matikan dengan `"SeedDemoData": false` sebelum pertama kali dijalankan bila ingin database kosong.

### Akun awal

| Peran | Username | Password |
| --- | --- | --- |
| Manajer Gerai | `manajer` | `manajer123` |
| Kasir | `kasir` | `kasir123` |
| Petugas Gudang | `gudang` | `gudang123` |
| Pengurus | `pengurus` | `pengurus123` |

Ganti password lewat **Pengaturan → Profil & Tema** setelah login.

### Menyiapkan database secara manual (opsional)

```sql
CREATE DATABASE STOKARA_DB;
GO
USE STOKARA_DB;
GO
-- jalankan isi Database/schema.sql lalu Database/seed.sql
```
Akun pengguna tetap dibuat otomatis oleh aplikasi (password perlu di-hash PBKDF2 di C#).

### Bila muncul "Database belum dapat digunakan"

Layar pembuka menampilkan langkah perbaikan (TC-14). Penyebab paling umum: layanan SQL Server belum berjalan, atau nama server di `appsettings.json` salah. Detail teknis ada di `%LocalAppData%\Stokara\logs`.

---

## 3. Struktur proyek

```
Stokara/
├── Models/            Entitas & model tampilan (Barang, Penjualan, SisaKuota, LineItem, …)
├── Repositories/      Query SQL berparameter (ADO.NET) + operasi stok/kuota atomik
├── Services/          Aturan bisnis, otorisasi per peran, transaksi database
├── ViewModels/        State UI, command, validasi inline (INotifyDataErrorInfo)
├── Views/             XAML (Window & UserControl) - code-behind hanya perilaku UI murni
├── Controls/          Custom control: StatCard, SimpleChart, attached properties, converter
├── Helpers/           ObservableObject, ViewModelBase, RelayCommand, AsyncRelayCommand
├── Infrastructure/    Koneksi DB, inisialisasi, sesi, log, hash password, tema, toast, dialog, cetak, ekspor
├── Resources/         Themes/Light.xaml, Themes/Dark.xaml (token "Bata dan Padi"), Styles.xaml
├── Database/          schema.sql, seed.sql
└── appsettings.json
```

Alur satu aksi: **View → ViewModel (command) → Service (aturan + otorisasi + transaksi) → Repository (SQL berparameter) → SQL Server**.

---

## 4. Fitur per halaman

| Halaman | Isi | Peran |
| --- | --- | --- |
| Dashboard | 6 kartu statistik beranimasi, grafik penjualan 7/30 hari, produk terlaris, kuota subsidi terpakai, stok kritis, perkiraan kedaluwarsa, aktivitas terbaru | Semua (isi menyesuaikan peran) |
| Kasir (F2) | Mode **Penjualan** / **Penyaluran Subsidi**, scan barcode + saran otomatis, harga anggota/umum, cek stok & kuota, uang pas/cepat, kembalian, struk | Manajer, Kasir |
| Riwayat Transaksi | Filter, detail, cetak ulang, **void** (Manajer), **Tutup Kasir** harian | Manajer, Kasir (hari ini), Pengurus |
| Barang | Cari/filter/urut server-side, pagination, form tambah/ubah dengan validasi inline, nonaktifkan, **kartu stok** | Semua (ubah: Manajer, Gudang tanpa harga) |
| Stok Masuk & Keluar | Penerimaan (Stok Awal, Pembelian, Subsidi, Konsinyasi) dan Retur/Rusak/Kedaluwarsa/Hilang | Manajer, Gudang |
| Stok Opname | Sesi → hitung fisik → ajukan → setujui/tolak oleh orang lain | Manajer, Gudang, Pengurus |
| Mitra | Anggota, Penerima Subsidi & aturan kuota, Pemasok, Penitip | Sesuai matriks PRD 4.1 |
| Konsinyasi | Ringkasan hutang, rincian per penitip, pelunasan FIFO, stok titipan | Manajer, Pengurus (bayar), Gudang (lihat) |
| Laporan | 6 laporan PRD + rekap harian kasir, grafik, cetak, ekspor CSV/Excel | Sesuai peran |
| Pengaturan | Profil & tema, kategori & satuan, pengguna, parameter & HET, log aktivitas, backup/restore | Sesuai peran |
| Bantuan | Panduan langkah demi langkah + daftar pintasan | Semua |

**Pintasan keyboard:** F2 kasir · F5 muat ulang · Ctrl+N baru · Ctrl+S simpan/bayar · Ctrl+F cari · Esc batal.

---

## 5. Cara menguji kasus uji PRD (bagian 16.1)

| TC | Cara mencoba |
| --- | --- |
| TC-01 | Kasir: tambah barang sampai melebihi stok → ditolak. |
| TC-02 | Barang → ubah Beras SPHP, isi harga di atas HET → error inline, tidak tersimpan. |
| TC-03 | Pengaturan → Parameter → HET LPG diturunkan di bawah harga jual → penyaluran LPG ditolak sampai harga disesuaikan. |
| TC-04 | Kasir mode Subsidi, Siti Aminah (kuota LPG 4, sudah 2): salurkan 3 → ditolak, sisa 2. |
| TC-05 | Cari NIK yang tidak terdaftar → tidak muncul, penyaluran tidak bisa disimpan. |
| TC-06 | Mode Penjualan, scan LPG 3 kg → diarahkan ke mode Penyaluran Subsidi. |
| TC-07 | Jual 3 Keripik (titip 8.000, jual 10.000) → Konsinyasi: hutang +24.000, komisi 6.000. |
| TC-08 | Konsinyasi → bayar sebagian → hutang tertua lunas lebih dulu. |
| TC-09 | Bayar melebihi sisa hutang → ditolak. |
| TC-10 | Terima 10 @1.200 untuk barang stok 10 @1.000 → HPP jadi 1.100 (lihat panel Barang). |
| TC-11 | Riwayat → void penyaluran hari ini → stok & kuota kembali. |
| TC-12 | Void penjualan titipan yang hutangnya sudah dibayar → ditolak. |
| TC-13 | Opname: fisik 22, sistem 25 → ajukan (gudang) → setujui (manajer) → stok −3, mutasi PENYESUAIAN. |
| TC-14 | Hentikan layanan SQLEXPRESS lalu buka aplikasi → pesan langkah perbaikan, tidak crash. |
| Akses | Login `kasir` → Pengaturan tidak menampilkan Parameter/HET; aksi terlarang ditolak service dan tercatat `AKSES_DITOLAK`. |

---

## 6. Catatan teknis

- **Atomik & konkurensi (BR-17, NFR-04):** setiap transaksi memakai satu `SqlTransaction`; stok dikurangi dengan `UPDATE … WHERE StokSaatIni >= @Jumlah` dan kuota dengan `… AND JumlahTerpakai + @Jumlah <= @Kuota`, lalu jumlah baris terdampak diperiksa.
- **Keamanan (NFR-05/06):** semua query berparameter; password PBKDF2-SHA256 100.000 iterasi + salt; login dikunci 5 menit setelah 5 kali gagal; NIK disamarkan untuk selain Manajer dan tidak pernah ditulis ke log.
- **Backup:** file `.bak` ditulis oleh layanan SQL Server, jadi pilih folder yang dapat diaksesnya (mis. `C:\StokaraBackup`).
- **Ekspor:** CSV UTF-8 dengan pemisah titik koma, langsung terbuka benar di Excel berbahasa Indonesia.
- **Logout otomatis:** isi `"IdleLogoutMinutes"` di `appsettings.json` (0 = mati).
