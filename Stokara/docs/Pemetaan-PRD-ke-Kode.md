# Pemetaan PRD v1.1 → Implementasi

Dokumen ini membantu bab Implementasi pada laporan: setiap kebutuhan PRD ditunjukkan letak kodenya.

## Kebutuhan fungsional

| ID | Kebutuhan | Kode utama |
| --- | --- | --- |
| FR-01 | Login/logout, kunci sementara | `Services/CoreServices.cs` (AuthService), `Views/LoginWindow.xaml`, `Repositories/UserRepository.cs` |
| FR-02 | Otorisasi per peran di service | `Infrastructure/Session.cs` (`Session.Require`), dipanggil di setiap service; menu difilter di `ViewModels/ShellViewModels.cs` |
| FR-03 | Manajemen pengguna | `UserService`, tab Pengguna di `Views/PengaturanView.xaml` |
| FR-04 | Kartu statistik dari DB | `Repositories/ReportRepository.cs` (`StatsAsync`), `Controls/StatCard.cs` |
| FR-05 | Grafik | `ReportRepository` (`PenjualanHarianAsync`, `TerlarisAsync`, `KuotaPerBarangAsync`), `Controls/SimpleChart.cs` |
| FR-06/07 | Stok kritis, perkiraan kedaluwarsa (A-09), aktivitas | `ReportRepository.StokKritisAsync`, `PerkiraanKedaluwarsaAsync` (window function SUM OVER) |
| FR-08 | Daftar barang: cari, filter, urut, pagination server-side | `BarangRepository.SearchAsync` (OFFSET/FETCH), `ViewModels/BarangViewModels.cs` |
| FR-09/10 | Tambah/ubah barang + validasi inline | `BarangService.Validate/SaveAsync`, `BarangFormViewModel` (INotifyDataErrorInfo), `Views/BarangFormWindow.xaml` |
| FR-11 | Master & mitra (soft delete) | `MitraService`, `MasterRepository`, `MitraRepository`, `Views/MitraView.xaml` |
| FR-12 | Aturan kuota & sisa per penerima | `KuotaRepository`, tab Penerima Subsidi & Kuota |
| FR-13 | Penerimaan multi-item, HPP rata-rata | `StokService.SavePenerimaanAsync`, `BarangRepository.IncreaseWithHppAsync` |
| FR-14 | Retur/rusak/hilang | `StokService.SavePengeluaranAsync` |
| FR-15 | Kartu stok | `BarangRepository.KartuStokAsync`, panel kanan `Views/BarangView.xaml` |
| FR-16 | Stok opname + persetujuan | `Services/OperasionalServices.cs` (OpnameService), `Views/OpnameView.xaml` |
| FR-17/18 | Kasir dua mode | `ViewModels/KasirViewModel.cs`, `Views/KasirView.xaml`, `Services/PenjualanService.cs` |
| FR-19 | Nomor dokumen otomatis | `TransaksiRepository.NextNumberAsync` (MERGE + HOLDLOCK pada tabel `NomorUrut`) |
| FR-20 | Cetak struk/bukti | `Infrastructure/DialogPrint.cs` (PrintService), `Views/ReceiptWindow.xaml` |
| FR-21/22 | Riwayat, void | `PenjualanService.ListAsync/VoidAsync`, `StokService.VoidPenerimaanAsync` |
| FR-23 | Tutup kasir | `PenjualanService.TutupHariAsync`, tab Tutup Kasir di `Views/RiwayatView.xaml` |
| FR-24-26 | Hutang penitip, pelunasan FIFO, stok titipan | `KonsinyasiRepository`, `KonsinyasiService.BayarAsync`, `Views/KonsinyasiView.xaml` |
| FR-27/28 | Laporan & ekspor | `Services/LaporanService.cs`, `Infrastructure/UiServices.cs` (ExportService), `Views/LaporanView.xaml` |
| FR-29 | Log aktivitas | `ActivityService`, `AktivitasRepository`, tab Log Aktivitas |
| FR-30 | Notifikasi, konfirmasi, state UI | `ToastService`, `Views/DialogWindow.xaml`, empty state di setiap halaman |
| FR-31 | Error DB & indikator status | `ErrorHandler`, `DbStatus`, header `Views/MainWindow.xaml`, `Views/StartupWindow.xaml` |
| FR-32 | Tema per pengguna | `ThemeService`, `Resources/Themes/*.xaml`, kolom `Users.Tema` |
| FR-33 | Backup/restore, panduan | `BackupService`, tab Backup & Restore, `Views/BantuanView.xaml` |

## Aturan bisnis yang ditegakkan di database (lapis kedua)

| Aturan | Constraint di `Database/schema.sql` |
| --- | --- |
| BR-02 stok ≥ 0 | `CK_Barang_Stok`, `CK_Mutasi_Nilai` |
| BR-03 unik | `UQ_Barang_SKU`, `UX_Barang_Barcode` (filtered), `UQ_Anggota_NIK`, `UQ_Penerima_NIK`, `UQ_*_Nomor` |
| BR-07 / A-02 subsidi | `CK_Barang_Subsidi`, `CK_Barang_SubsidiMilik` |
| BR-09 kuota | `UQ_AturanKuota`, `UQ_PemakaianKuota`, `CK_Pemakaian_Jumlah` + UPDATE bersyarat |
| BR-12/13 konsinyasi | `CK_Barang_Titipan`, `CK_Hutang_Bayar`, `UQ_Hutang_PjDetail` |
| BR-15 penyetuju ≠ pengaju | `CK_Opname_Penyetuju` |
| A-10 jenis transaksi | `CK_Penjualan_Pihak` |

## Fitur Rich UI (PRD 10.3)

| Fitur | Implementasi |
| --- | --- |
| MVVM & data binding | Semua View terikat ke ViewModel lewat `DataTemplate` di `App.xaml` |
| Validasi inline | `ViewModelBase` (INotifyDataErrorInfo) + `ErrorTemplate` di `Styles.xaml` |
| Custom control | `StatCard` (angka beranimasi), `SimpleChart` (garis/batang/progres, hover tooltip, animasi tumbuh) |
| Tema dinamis | Pertukaran ResourceDictionary saat runtime, semua warna `DynamicResource` |
| Animasi | Transisi halaman (`PageTransition`), toast slide-in, kartu statistik, grafik |
| Interaksi | Autocomplete barang (Popup), dialog modal bertema, pintasan keyboard global, segmented control |
| Responsivitas | `async/await` di semua akses DB, virtualisasi DataGrid, pagination server |

## Bukti untuk laporan (saran screenshot)

1. Layar pembuka & login · 2. Dashboard terang dan gelap · 3. Kasir mode Penjualan dengan saran barang ·
4. Kasir mode Subsidi saat kuota ditolak · 5. Form barang dengan error inline · 6. Kartu stok ·
7. Opname diajukan → disetujui · 8. Konsinyasi setelah pelunasan · 9. Laporan + file CSV di Excel ·
10. Log aktivitas berisi `AKSES_DITOLAK` · 11. Pesan saat SQL Server dimatikan.
