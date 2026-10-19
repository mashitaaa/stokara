# PRD STOKARA - Sistem Inventori dan Penyaluran Barang Gerai Koperasi Desa Merah Putih

*Product Requirements Document · Aplikasi Desktop Rich UI berbasis WPF, C#, dan SQL Server · Studi Kasus: Gerai Koperasi Desa/Kelurahan Merah Putih · Versi 1.1*

| Informasi Dokumen | Deskripsi |
| --- | --- |
| Nama Produk | STOKARA - Sistem Inventori dan Penyaluran Barang Gerai Koperasi Desa Merah Putih |
| Studi Kasus | Pengelolaan stok, penjualan, penyaluran barang bersubsidi, dan barang konsinyasi pada gerai Koperasi Desa/Kelurahan Merah Putih |
| Jenis Dokumen | Product Requirements Document (PRD) |
| Versi | 1.1 - Konsolidasi asumsi dan pemadatan cakupan |
| Platform | Desktop Windows - WPF, berjalan offline pada satu komputer gerai |
| Teknologi Utama | C#, XAML, .NET (LTS), SQL Server Express, Microsoft.Data.SqlClient (ADO.NET), pola MVVM |
| Mata Kuliah | Pemrograman Berbasis Kerangka Kerja - Final Project (Aplikasi Desktop Rich UI) |
| Penyusun | \[Nama / NRP penyusun\] |
| Lokasi Studi Kasus | \[Nama Kopdes, desa, kecamatan, kabupaten - isi dari hasil wawancara\] |
| Tanggal | \[Isi tanggal\] |

## Riwayat Revisi

| Versi | Tanggal | Perubahan | Penyusun |
| --- | --- | --- | --- |
| 0.1 | \[tanggal\] | Kerangka dan analisis masalah (versi gudang umum) | \[nama\] |
| 1.0 | \[tanggal\] | Disesuaikan ke konteks Kopdes Merah Putih: subsidi, konsinyasi, keanggotaan, dan pelaporan | \[nama\] |
| 1.1 | \[tanggal\] | Seluruh hal yang rancu ditetapkan sebagai asumsi desain tunggal (bagian 3.3); halaman dipadatkan dari 23 menjadi 11, peran dari 5 menjadi 4, kebutuhan fungsional dari 45 menjadi 33, laporan dari 11 menjadi 6; skema kuota, konsinyasi, HPP, dan void diperbaiki; ditambahkan kebutuhan Rich UI dan artefak perancangan | \[nama\] |

## Daftar Istilah

| Istilah | Arti |
| --- | --- |
| Kopdes / KDKMP | Koperasi Desa/Kelurahan Merah Putih. |
| Gerai | Unit usaha ritel milik Kopdes tempat barang dijual atau disalurkan ke warga. |
| Simkopdes | Sistem Informasi Manajemen Koperasi Desa/Kelurahan Merah Putih, sistem nasional dari Kemenkop. |
| Anggota | Warga yang tercatat sebagai anggota koperasi. |
| Barang Subsidi | Barang yang harga dan penyalurannya diatur pemerintah, mis. pupuk bersubsidi, LPG 3 kg, beras SPHP. |
| HET | Harga Eceran Tertinggi; batas harga jual barang subsidi. |
| Kuota | Jumlah maksimum barang subsidi yang boleh diterima satu penerima pada satu periode. |
| Penerima Subsidi | Warga atau petani yang berhak menerima barang subsidi; tidak harus anggota. |
| Konsinyasi / Titipan | Barang milik penitip yang dijual gerai; penitip dibayar setelah barang terjual. |
| Penitip | Pemilik barang konsinyasi (pemasok BUMN/BUMD, UMKM, atau warga desa). |
| Harga Titip | Harga yang menjadi hak penitip per unit terjual. |
| HPP Rata-rata | Harga pokok rata-rata tertimbang per unit barang milik koperasi. |
| SKU | Kode unik internal barang. |
| Kartu Stok | Riwayat seluruh mutasi stok per barang. |
| Stok Opname | Penghitungan fisik barang untuk dicocokkan dengan stok di sistem. |
| Void | Pembatalan transaksi dengan mutasi balik, tanpa menghapus riwayat. |

---

## 1. Ringkasan Eksekutif

STOKARA adalah aplikasi desktop yang membantu pengelola gerai Koperasi Desa Merah Putih mencatat stok, penjualan, penyaluran barang bersubsidi, dan barang konsinyasi secara tertib dan dapat ditelusuri. Produk memakai pendekatan Rich UI: navigasi sidebar, kartu statistik, grafik, tabel data, peringatan stok kritis, layar kasir cepat, validasi langsung pada form, serta tema gelap/terang.

Gerai Kopdes berbeda dari toko biasa. Gerai menjual barang bebas sekaligus barang subsidi yang dibatasi HET dan kuota per penerima. Gerai juga menampung barang titipan yang pemiliknya harus dibayar setelah laku, membedakan harga anggota dan non-anggota, dan wajib dapat dipertanggungjawabkan kepada pengurus. Pencatatan stok yang akurat dan dapat ditelusuri adalah inti produk ini.

### 1.1 Tujuan Produk

1. Mencatat seluruh pergerakan stok gerai pada satu aplikasi yang tetap berjalan tanpa internet.
2. Memastikan penyaluran barang subsidi mematuhi HET, daftar penerima, dan kuota.
3. Memisahkan barang konsinyasi dari barang milik koperasi dan menghitung hutang ke penitip secara otomatis.
4. Menerapkan harga anggota dan harga umum secara otomatis.
5. Menyajikan peringatan dan ringkasan yang mudah dibaca pengurus dengan literasi digital terbatas.
6. Menerapkan struktur kode MVVM yang rapi agar mudah diuji dan dikembangkan.

### 1.2 Pernyataan Produk

Untuk manajer gerai, kasir, petugas gudang, dan pengurus Kopdes yang membutuhkan pencatatan stok dan penyaluran barang yang akurat meski internet tidak stabil, STOKARA menyediakan aplikasi desktop offline-first yang menggabungkan kasir cepat, kontrol barang subsidi, pengelolaan konsinyasi, dan laporan sederhana dalam satu lingkungan kerja yang mudah dipelajari.

---

## 2. Latar Belakang dan Permasalahan

### 2.1 Konteks Program Kopdes Merah Putih

Menurut siaran pers Kemenko Pangan tanggal 20 Juli 2026, [83.381 koperasi telah berbadan hukum dan 80.977 sudah memiliki akun Simkopdes](https://kemenkopangan.go.id/assets/images/artikel/f7c2a9f42a1943e3f02c73c2c8c2e29e.pdf). Kopdes [dijadwalkan beroperasi serentak pada 1 Oktober 2026](https://www.rctiplus.com/news/detail/okezone/5483154/kopdes-merah-putih-beroperasi-1-oktober--ini-daftar-barang-yang-dijual) dengan pasokan barang dari BUMN, termasuk pupuk subsidi, beras SPHP, bantuan pangan, dan gas 3 kg. Kopdes juga [diarahkan menjadi penyalur barang subsidi dan bantuan sosial](https://www.idnfinancials.com/id/news/66153/prabowo-backs-kopdes-for-aid-distribution-and-farm-collection). Untuk LPG 3 kg, Kopdes [berperan sebagai sub-pangkalan](https://www.cnbcindonesia.com/news/20250723162208-4-651974/lpg-3-kg-akan-dijual-di-semua-koperasi-merah-putih-ini-kata-esdm), dan gerai contoh seperti Kopdes Melawai [memakai sistem konsinyasi dengan BUMN/BUMD](https://katadata.co.id/amp/foto/68f1f1c18dd5e/foto-koperasi-merah-putih-melawai-hadirkan-sembako-dengan-harga-terjangkau/).

Peraturan teknis penyaluran masih dapat berubah. Karena itu, seluruh aturan penyaluran (HET, kuota, periode) diperlakukan sebagai parameter yang dapat dikonfigurasi, bukan nilai tetap.

### 2.2 Kondisi Saat Ini (Hipotesis yang Diverifikasi Lewat Wawancara)

- Banyak pengelola gerai baru pertama kali mengelola usaha ritel dan memiliki literasi digital terbatas; penelitian di beberapa desa menyebut [rendahnya literasi digital pengurus sebagai hambatan utama](https://www.ejournal.unma.ac.id/index.php/bernas/article/view/15622/7246).
- Pencatatan stok dan penjualan harian masih manual atau terpisah dari sistem nasional.
- Barang subsidi, barang bebas, dan barang titipan tercampur dalam pencatatan, sehingga kepatuhan HET/kuota, hutang ke penitip, dan laba koperasi sulit dibuktikan.
- Stok opname jarang dilakukan, dan tidak ada riwayat siapa mengubah stok dan kapan.
- Koneksi internet di sebagian desa tidak stabil.

### 2.3 Permasalahan dan Solusi

| No | Masalah | Dampak | Solusi STOKARA |
| --- | --- | --- | --- |
| 1 | Selisih stok fisik dan catatan | Kerugian, barang tidak terlacak | Stok hanya berubah lewat mutasi; kartu stok; stok opname |
| 2 | Barang subsidi dijual di atas HET atau melebihi kuota | Subsidi tidak tepat sasaran | Validasi HET, daftar penerima, dan kuota di service |
| 3 | Barang titipan tercampur dengan milik koperasi | Hutang dan laba tidak jelas | Kepemilikan per barang; hutang penitip otomatis |
| 4 | Aplikasi yang rumit tidak dipakai | Pencatatan kembali manual | Layar kasir cepat, bahasa awam, sedikit langkah |
| 5 | Internet tidak stabil | Pencatatan terhenti | Offline-first dengan database lokal |
| 6 | Laporan manual dan tanpa jejak perubahan | Sulit dipantau dan diaudit | Dashboard, laporan otomatis, log aktivitas |

### 2.4 Pemangku Kepentingan

| Pemangku kepentingan | Kepentingan |
| --- | --- |
| Pengurus dan Pengawas Kopdes | Memantau kinerja gerai, hutang penitip, dan kepatuhan subsidi. |
| Manajer Gerai | Menjalankan operasional harian dan mengelola sistem. |
| Kasir dan Petugas Gudang | Melayani penjualan/penyaluran serta menerima dan menghitung barang. |
| Anggota dan warga | Memperoleh barang dengan harga wajar dan penyaluran yang adil. |
| Pemasok BUMN/BUMD dan penitip | Mengirim barang dan menerima pembayaran sesuai penjualan. |
| Dosen/Penilai | Menilai kelengkapan rancangan dan kualitas implementasi Rich UI. |

### 2.5 Posisi terhadap Simkopdes

Simkopdes dipakai sebagai [dashboard pemantauan stok, transaksi, dan distribusi barang subsidi](https://goodstats.id/article/produk-kopdes-merah-putih-yang-paling-banyak-dibeli-masyarakat-7YSs5) dan tersedia juga dalam bentuk [aplikasi mobile](https://apps.apple.com/id/app/simkopdes/id6749216676). STOKARA **tidak menggantikan** Simkopdes. STOKARA berperan sebagai alat kerja operasional di komputer gerai: bisa dipakai tanpa internet, mendukung input cepat dengan keyboard dan barcode scanner, punya kontrol stok yang rinci, dan mengelola konsinyasi. Data dikirim ke pengurus dalam bentuk rekap ekspor. Risiko pencatatan ganda dibahas di bagian 18.

### 2.6 Alasan Memilih Aplikasi Desktop

Gerai di desa dapat memiliki internet yang lemah, komputer kasir membutuhkan input cepat dengan barcode scanner dan printer struk, data gerai tetap tersimpan di komputer koperasi, dan tugas final ini mensyaratkan aplikasi desktop Rich UI.

---

## 3. Sasaran, Batasan, dan Asumsi

### 3.1 Sasaran dan Indikator Keberhasilan

| Sasaran | Indikator (target awal) |
| --- | --- |
| Pencatatan stok akurat | Saldo kartu stok = stok barang; setelah opname, ≥ 98% jenis barang tanpa selisih. |
| Kepatuhan subsidi | 0 penyaluran yang melanggar HET, status penerima, atau kuota pada data uji. |
| Konsinyasi tertib | Hutang penitip sama dengan hitungan manual pada data uji (selisih 0). |
| Kemudahan penggunaan | Penjualan 3 item ≤ 1 menit; ≥ 80% pengguna uji menyelesaikan tugas utama tanpa bantuan. |
| Dashboard dinamis | Seluruh kartu dan grafik bersumber dari query database. |
| Keandalan dan keamanan | Berjalan tanpa internet; error database tidak membuat aplikasi crash; akses dibatasi per peran di service. |
| Kualitas rilis | 100% kasus uji berprioritas Must lulus; pencarian barang ≤ 2 detik pada data uji. |

### 3.2 Batasan

- Rilis awal melayani **satu gerai** dengan satu komputer utama yang menjalankan SQL Server Express. Komputer lain di jaringan lokal boleh terhubung, tetapi tidak wajib.
- Satu satuan dasar per barang (pcs, kg, tabung, sak).
- Pembayaran **hanya tunai**.
- Verifikasi penerima subsidi dilakukan manual oleh petugas dengan mencocokkan identitas. Tidak ada integrasi dengan data kependudukan atau sistem pemerintah.
- HET, kuota, dan parameter lain diinput oleh manajer berdasarkan ketentuan resmi yang berlaku.

### 3.3 Asumsi Desain

Hal-hal yang sebelumnya rancu ditetapkan sebagai **satu asumsi** berikut agar desain dapat dibangun. Asumsi ini dapat direvisi bila hasil wawancara (Lampiran C) menunjukkan kondisi lain, tetapi selama belum direvisi, asumsi inilah yang berlaku.

| ID | Topik | Asumsi yang dipakai | Konsekuensi pada desain |
| --- | --- | --- | --- |
| A-01 | Penetapan harga | Harga jual **dikunci di master barang**. Kasir tidak dapat mengubah harga di layar kasir. | Validasi HET dilakukan saat menyimpan barang dan diperiksa ulang oleh service saat transaksi. |
| A-02 | Harga barang subsidi | Barang subsidi memiliki **satu harga jual** untuk semua penerima, dan harga itu ≤ HET. | Untuk barang subsidi, HargaAnggota = HargaUmum = HargaSubsidi. |
| A-03 | Struktur kuota | Kuota ditetapkan sebagai **aturan per Barang × Jenis Penerima × Periode**. Pemakaian dicatat **per penerima**. | Tabel AturanKuota dan PemakaianKuota terpisah (bagian 11). Baris pemakaian dibuat otomatis saat penyaluran pertama pada periode tersebut. |
| A-04 | Periode kuota | Default **bulan kalender**, dapat diubah manajer di Pengaturan. | Kolom Periode berformat `YYYY-MM`. |
| A-05 | Skema konsinyasi | Hanya **harga titip tetap per barang**; tidak ada skema komisi persentase. Komisi koperasi = harga jual − harga titip. | Field HargaTitip ada di Barang. HutangPenitip menyimpan salinan harga titip saat barang terjual, sehingga perubahan harga titip hanya berlaku untuk penjualan berikutnya. |
| A-06 | Kepemilikan barang | Satu barang hanya punya satu kepemilikan (MILIK atau TITIPAN) dan maksimal satu penitip. Produk yang sama dari sumber berbeda memakai SKU berbeda. | Tidak diperlukan pelacakan kepemilikan per unit. |
| A-07 | Pelunasan penitip | Pembayaran ke penitip dialokasikan otomatis ke hutang **tertua lebih dulu (FIFO)**. | Field JumlahDibayar dan Status (BELUM/SEBAGIAN/LUNAS) ada di HutangPenitip. |
| A-08 | Penilaian persediaan | **Rata-rata tertimbang**, hanya untuk barang milik koperasi. | Field HargaPokokRata di Barang diperbarui setiap penerimaan. PenjualanDetail menyimpan salinan HargaPokok untuk menghitung laba kotor. |
| A-09 | Kedaluwarsa | **Tanpa pelacakan batch.** Tanggal kedaluwarsa dicatat per penerimaan. Sistem mengasumsikan barang keluar dengan urutan masuk-pertama-keluar-pertama, sehingga sisa stok dianggap berasal dari penerimaan terbaru. | Peringatan kedaluwarsa bersifat estimasi dan diberi label "perkiraan". Prioritasnya Should. |
| A-10 | Jenis transaksi | Satu transaksi hanya satu jenis: **PENJUALAN** (umum/anggota, boleh berisi barang milik dan titipan) atau **PENYALURAN_SUBSIDI** (hanya barang subsidi, wajib ada penerima). Keduanya tidak dicampur. | Kasir memiliki dua mode di satu layar. Penjualan.AnggotaId dan PenerimaId tidak pernah terisi bersamaan. |
| A-11 | Penerima vs anggota | Penerima subsidi **tidak harus anggota**. Penerima boleh ditautkan ke data anggota bila memang anggota. | PenerimaSubsidi.AnggotaId bersifat opsional. |
| A-12 | Retur pelanggan | **Tidak didukung** di rilis awal. Koreksi transaksi pelanggan dilakukan lewat void penuh. | Sumber penerimaan RETUR_PELANGGAN dihapus. |
| A-13 | Aturan void | Void hanya untuk transaksi **hari berjalan yang belum ditutup**. Void penjualan titipan ditolak bila hutang terkait sudah dibayar sebagian atau penuh. Void penerimaan ditolak bila stok saat ini kurang dari jumlah yang diterima. | Konflik dengan Tutup Kasir hilang. Koreksi setelah hari ditutup dilakukan lewat stok opname. |
| A-14 | Stok awal | Stok awal dimasukkan lewat penerimaan dengan sumber **STOK_AWAL**. | Stok awal tetap tercatat di kartu stok seperti mutasi lainnya. |
| A-15 | Hasil tani | **Di luar lingkup** rilis awal. | Tidak ada sumber penerimaan HASIL_TANI. |
| A-16 | Hak akses per field | Petugas Gudang boleh mengubah data barang **kecuali** harga, HET, dan harga titip. Field tersebut tampil read-only bagi Petugas Gudang dan juga ditolak oleh service. | Pemeriksaan izin dilakukan per field di BarangService. |
| A-17 | Tutup kasir | Kasir menginput **modal awal** dan **uang fisik** saat menutup hari. Uang seharusnya = modal awal + total penjualan tunai. | Tabel TutupHari menyimpan ModalAwal, UangSeharusnya, UangFisik, dan Selisih. |
| A-18 | Integrasi Simkopdes | Tidak ada integrasi. Rekap diekspor ke CSV/Excel dengan format STOKARA sendiri. | Format ekspor disesuaikan setelah wawancara. |
| A-19 | Preferensi tema | Tema disimpan **per pengguna**. | Field Tema ada di Users. |

---

## 4. Pengguna dan Hak Akses

Peran Pengurus dan Pengawas digabung menjadi satu peran **Pengurus**, karena kebutuhan keduanya sama-sama memantau dan menyetujui.

| Peran | Kebutuhan utama |
| --- | --- |
| Manajer Gerai (Admin) | Akses penuh: master data, harga/HET/kuota, pengguna, pengaturan, void, persetujuan opname. |
| Kasir | Melayani penjualan dan penyaluran subsidi, menutup kasir. |
| Petugas Gudang | Menerima barang, mencatat retur/barang rusak, menginput stok opname. |
| Pengurus (Ketua/Bendahara/Pengawas) | Membaca dashboard, laporan, dan log; menyetujui opname; mencatat pelunasan penitip. |

### 4.1 Matriks Hak Akses per Halaman

| Halaman | Manajer | Kasir | Petugas Gudang | Pengurus |
| --- | --- | --- | --- | --- |
| Dashboard | Penuh | Ringkas¹ | Ringkas² | Penuh |
| Kasir (Penjualan & Subsidi) | Ya | Ya | - | - |
| Riwayat Transaksi | Ya + void | Hari ini milik sendiri + tutup kasir | - | Lihat |
| Barang & Kartu Stok | Ya | Lihat | Ubah (tanpa harga, A-16) | Lihat |
| Stok Masuk & Keluar | Ya | - | Ya | Lihat |
| Stok Opname | Ya + setujui | - | Input | Lihat + setujui |
| Mitra (Anggota, Penerima & Kuota, Pemasok, Penitip) | Ya | Cari penerima, tambah anggota | Lihat pemasok/penitip | Lihat |
| Konsinyasi | Ya | - | Lihat | Lihat + catat pelunasan |
| Laporan | Semua | Rekap harian sendiri | Laporan stok | Semua |
| Pengaturan | Semua tab | Profil & tema | Profil & tema | Profil, tema, log aktivitas |

¹ Penjualan hari ini milik kasir tersebut dan daftar stok kritis.  ² Stok kritis, perkiraan kedaluwarsa, dan penerimaan terbaru.

Menu yang tidak diizinkan tidak ditampilkan, dan aksinya **tetap diblokir di service**.

---

## 5. Ruang Lingkup

### 5.1 In Scope - Rilis Awal

- **Akses:** login/logout, peran, manajemen pengguna, log aktivitas.
- **Dashboard:** kartu statistik, grafik, panel stok kritis dan perkiraan kedaluwarsa.
- **Master dan mitra:** barang, kategori, satuan, pemasok, penitip, anggota, penerima subsidi, aturan kuota.
- **Stok:** penerimaan (stok awal, pembelian, subsidi, konsinyasi), pengeluaran non-penjualan (retur ke pemasok/penitip, rusak, kedaluwarsa, hilang), kartu stok, stok opname.
- **Transaksi:** kasir dengan mode Penjualan (umum/anggota) dan Penyaluran Subsidi, struk, riwayat transaksi, void, tutup kasir.
- **Konsinyasi:** stok titipan, hutang penitip otomatis, pelunasan.
- **Laporan:** 6 laporan dengan ekspor CSV/Excel.
- **Sistem:** tema gelap/terang, notifikasi, penanganan error, indikator status database, backup/restore, panduan singkat.

### 5.2 Out of Scope

Simpan pinjam, akuntansi/SHU lengkap, unit usaha lain (klinik, agen bank); integrasi dengan Simkopdes, BUMN, data kependudukan, atau pembayaran non-tunai; retur pelanggan dan penerimaan hasil tani; multi-gerai; pelacakan batch/serial dan lokasi rak; aplikasi mobile/web; pemulihan kata sandi lewat email.

---

## 6. Struktur Navigasi dan Halaman

| No | Halaman | Isi | Penggabungan dari v1.0 |
| --- | --- | --- | --- |
| 1 | Login | Logo, username, password, tampilkan password, tombol Masuk. | - |
| 2 | Dashboard | Kartu statistik, grafik, panel peringatan, aktivitas terbaru. | - |
| 3 | Kasir | Toggle mode **Penjualan / Penyaluran Subsidi**, kolom scan/cari, keranjang, pilih anggota atau penerima, sisa kuota, total, bayar, cetak struk. | Kasir + Penyaluran Subsidi |
| 4 | Riwayat Transaksi | Daftar dan filter transaksi, detail, cetak ulang, void, tab **Tutup Kasir**. | Penjualan + Tutup Kasir |
| 5 | Barang | Tabel, cari, filter, form barang (dialog), panel detail berisi **Kartu Stok**. | Barang + Form Barang + Kartu Stok |
| 6 | Stok Masuk & Keluar | Tab **Penerimaan** dan tab **Retur & Barang Rusak**, masing-masing dengan form multi-item. | Penerimaan + Form Penerimaan + Retur |
| 7 | Stok Opname | Daftar sesi (Draft → Diajukan → Disetujui/Ditolak), input hitung fisik, selisih. | - |
| 8 | Mitra | Tab Anggota, Penerima Subsidi & Kuota, Pemasok, Penitip. | Anggota + Penerima Subsidi + sebagian Master Data |
| 9 | Konsinyasi | Tab Stok Titipan dan Hutang & Pelunasan. | - |
| 10 | Laporan | Jenis laporan, periode, grafik, tabel, cetak, ekspor. | - |
| 11 | Pengaturan | Tab Profil & Tema, Kategori & Satuan, Pengguna, Parameter (HET/kuota/ambang), Log Aktivitas, Backup & Restore. | Master Data + Pengguna + Log + Pengaturan |

### 6.1 Layout Utama

- **Sidebar kiri:** menu dikelompokkan menjadi Harian (Dashboard, Kasir, Riwayat), Stok (Barang, Masuk & Keluar, Opname), Mitra (Mitra, Konsinyasi), serta Laporan dan Pengaturan.
- **Header:** judul halaman, indikator status database, toggle tema, nama dan peran pengguna, tombol keluar.
- **Area konten:** kartu statistik di atas, grafik dan tabel di bawahnya, panel peringatan di kanan.
- **Mode Kasir:** layar penuh dengan tombol besar dan fokus otomatis ke kolom barcode.

---

## 7. Kebutuhan Fungsional

Prioritas memakai skema MoSCoW: Must, Should, Could.

| ID | Modul | Persyaratan | Prioritas |
| --- | --- | --- | --- |
| FR-01 | Akses | Login/logout; tolak kredensial salah dan akun nonaktif; batasi percobaan gagal berulang. | Must |
| FR-02 | Akses | Otorisasi per peran pada menu **dan** service, termasuk izin per field harga (A-16). | Must |
| FR-03 | Akses | Manajer menambah, menonaktifkan, mengubah peran, dan mereset password pengguna. | Must |
| FR-04 | Dashboard | Kartu statistik (bagian 9.2) dihitung dari database. | Must |
| FR-05 | Dashboard | Grafik penjualan 7/30 hari, produk terlaris, dan kuota subsidi terpakai vs tersedia. | Must |
| FR-06 | Dashboard | Panel stok kritis (Menipis/Habis). | Must |
| FR-07 | Dashboard | Panel perkiraan kedaluwarsa (A-09) dan aktivitas terbaru. | Should |
| FR-08 | Barang | Daftar dengan pagination; cari SKU/nama/barcode tanpa membedakan huruf besar-kecil; filter kategori, jenis (bebas/subsidi/titipan), dan status stok; urut kolom. | Must |
| FR-09 | Barang | Tambah, ubah, dan nonaktifkan barang. Stok tidak dapat diubah dari form barang. | Must |
| FR-10 | Validasi | Field wajib; keunikan SKU, barcode, NIK, dan nomor anggota; barang subsidi wajib punya HET dan harga ≤ HET; barang titipan wajib punya penitip dan harga titip ≤ harga jual. Validasi tampil langsung di form. | Must |
| FR-11 | Mitra | CRUD (soft delete) untuk kategori, satuan, pemasok, penitip, anggota, dan penerima subsidi. | Must |
| FR-12 | Mitra | Aturan kuota per barang × jenis penerima × periode; tampilkan pemakaian dan sisa per penerima. | Must |
| FR-13 | Stok | Penerimaan multi-item dengan sumber STOK_AWAL, PEMBELIAN, SUBSIDI, atau KONSINYASI. Stok, HPP rata-rata, dan mutasi diperbarui dalam satu transaksi database. | Must |
| FR-14 | Stok | Pengeluaran non-penjualan (retur ke pemasok/penitip, rusak, kedaluwarsa, hilang) dengan alasan wajib. | Must |
| FR-15 | Stok | Kartu stok per barang dan periode, dengan saldo berjalan. | Must |
| FR-16 | Stok | Stok opname: sesi, salinan stok sistem, input fisik, selisih otomatis, persetujuan oleh orang yang berbeda dari pengaju, dan mutasi PENYESUAIAN. | Should |
| FR-17 | Kasir | Mode Penjualan: scan/cari barang (mendukung barcode scanner keyboard-wedge), keranjang, harga anggota/umum otomatis, tolak jumlah melebihi stok, bayar tunai, dan hitung kembalian. | Must |
| FR-18 | Kasir | Mode Penyaluran Subsidi: cari penerima lewat NIK/nama, tampilkan status dan sisa kuota; tolak bila penerima tidak aktif, harga > HET, jumlah > sisa kuota, atau stok tidak cukup. | Must |
| FR-19 | Kasir | Nomor dokumen otomatis dan unik (mis. PJ-20261001-0001, SB-…, PN-…). | Must |
| FR-20 | Kasir | Cetak/pratinjau struk dan bukti penyaluran. | Should |
| FR-21 | Transaksi | Riwayat transaksi dengan filter, detail, dan cetak ulang. | Must |
| FR-22 | Transaksi | Void oleh manajer dengan alasan, mengikuti aturan A-13; membuat mutasi balik, memulihkan kuota, dan membatalkan hutang penitip. | Should |
| FR-23 | Transaksi | Tutup kasir harian (A-17); hari yang sudah ditutup tidak menerima transaksi atau void. | Should |
| FR-24 | Konsinyasi | Saat barang titipan terjual, sistem otomatis membuat hutang penitip (salinan harga titip) dan menghitung komisi koperasi. | Must |
| FR-25 | Konsinyasi | Catat pelunasan dengan alokasi FIFO (A-07); tolak pembayaran yang melebihi sisa hutang. | Must |
| FR-26 | Konsinyasi | Stok titipan tampil terpisah dan tidak masuk nilai persediaan koperasi. | Must |
| FR-27 | Laporan | Enam laporan (bagian 13) dengan filter periode. | Must |
| FR-28 | Laporan | Ekspor laporan ke CSV/Excel. | Should |
| FR-29 | Sistem | Log aktivitas: login, CRUD, transaksi, void, penyesuaian, dan perubahan HET/kuota. | Must |
| FR-30 | Sistem | Notifikasi berhasil/gagal, dialog konfirmasi, serta state loading, kosong, dan akses ditolak. | Must |
| FR-31 | Sistem | Penanganan error database tanpa crash, serta indikator status koneksi database di header. | Must |
| FR-32 | Sistem | Tema gelap/terang yang disimpan per pengguna. | Should |
| FR-33 | Sistem | Backup dan restore database secara manual dari Pengaturan, serta panduan singkat (tooltip dan halaman bantuan) untuk alur kasir, penerimaan, dan subsidi. | Should |

### 7.1 Aturan Bisnis

| ID | Aturan |
| --- | --- |
| BR-01 | Stok berubah **hanya** lewat mutasi. StokSaatIni dan MutasiStok diperbarui dalam transaksi database yang sama. |
| BR-02 | Pengeluaran tidak boleh melebihi stok tersedia; stok tidak boleh negatif. |
| BR-03 | SKU, barcode, nomor anggota, NIK anggota, NIK penerima, username, dan nomor dokumen bersifat unik. |
| BR-04 | Status Menipis jika 0 < stok ≤ stok minimum; Habis jika stok = 0; selain itu Aman. |
| BR-05 | Transaksi yang sudah disimpan tidak dapat diubah atau dihapus. Koreksi dilakukan lewat void (A-13) atau penyesuaian opname. |
| BR-06 | Jumlah item harus > 0 dan harga ≥ 0. |
| BR-07 | Harga barang subsidi ≤ HET, diperiksa saat menyimpan barang **dan** saat transaksi. Bila HET diturunkan di bawah harga jual, barang tersebut tidak dapat disalurkan sampai harganya disesuaikan. |
| BR-08 | Penyaluran subsidi hanya untuk penerima terdaftar yang aktif, dan transaksinya hanya berisi barang subsidi (A-10). |
| BR-09 | Sisa kuota = JumlahKuota (aturan untuk jenis penerima) − JumlahTerpakai (penerima tersebut, periode berjalan). Jumlah yang disalurkan tidak boleh melebihi sisa kuota. Void memulihkan kuota. |
| BR-10 | Harga anggota hanya berlaku untuk anggota aktif. |
| BR-11 | Barang titipan bukan milik koperasi dan tidak masuk nilai persediaan. |
| BR-12 | Hutang penitip = jumlah terjual × harga titip saat terjual. Komisi = (harga jual − harga titip) × jumlah. |
| BR-13 | Pelunasan tidak boleh melebihi total sisa hutang penitip dan dialokasikan FIFO. |
| BR-14 | HPP rata-rata baru = (stok lama × HPP lama + jumlah masuk × harga beli) ÷ (stok lama + jumlah masuk). Nilai persediaan = Σ stok × HPP rata-rata untuk barang milik koperasi. |
| BR-15 | Penyesuaian opname wajib disertai alasan dan disetujui oleh Manajer/Pengurus yang bukan pengaju. |
| BR-16 | Data master yang pernah dipakai pada transaksi hanya boleh dinonaktifkan, bukan dihapus. |
| BR-17 | Penyimpanan transaksi (header, detail, mutasi, stok, kuota, hutang) dilakukan secara atomik. |
| BR-18 | Pembuat dan stempel waktu transaksi dicatat otomatis dan tidak dapat diedit. |
| BR-19 | HET, aturan kuota, periode kuota, dan ambang peringatan adalah parameter yang dapat diubah manajer; setiap perubahan tercatat di log. |

---

## 8. User Stories dan Kriteria Penerimaan

| ID | User story | Kriteria penerimaan utama |
| --- | --- | --- |
| US-01 | Sebagai pengurus/manajer, saya ingin melihat ringkasan kondisi gerai agar dapat memutuskan dengan cepat. | Kartu dan grafik bersumber dari database dan mengikuti periode; panel peringatan tampil; data kosong menampilkan nol atau empty state. |
| US-02 | Sebagai kasir, saya ingin mencatat penjualan dengan cepat agar antrean tidak menumpuk. | Barang dapat dicari/discan; harga sesuai jenis pelanggan; jumlah melebihi stok ditolak; setelah disimpan stok berkurang dan struk dapat dicetak. |
| US-03 | Sebagai kasir, saya ingin menyalurkan barang subsidi hanya kepada penerima yang berhak. | Penerima dicari lewat NIK/nama dengan sisa kuota tampil; penerima tidak aktif, harga > HET, dan jumlah > sisa kuota ditolak dengan alasan jelas; setelah disimpan kuota terpakai bertambah. |
| US-04 | Sebagai petugas gudang, saya ingin mencatat barang masuk (termasuk titipan) agar stok bertambah otomatis. | Sumber dipilih; nomor dokumen otomatis; stok, HPP, dan kartu stok diperbarui; barang titipan tidak menambah nilai persediaan; jika penyimpanan gagal, tidak ada perubahan sebagian. |
| US-05 | Sebagai bendahara, saya ingin mengetahui dan melunasi hutang ke penitip. | Hutang bertambah otomatis saat barang titipan terjual; rincian per penitip tampil; pelunasan dialokasikan FIFO; pembayaran berlebih ditolak. |
| US-06 | Sebagai manajer/petugas gudang, saya ingin mengelola data barang. | Validasi langsung di form; SKU/barcode duplikat ditolak; barang subsidi wajib punya HET; field harga terkunci untuk petugas gudang. |
| US-07 | Sebagai manajer, saya ingin mengelola penerima dan aturan kuota. | NIK duplikat ditolak; kuota ditetapkan per barang, jenis penerima, dan periode; perubahan tercatat di log. |
| US-08 | Sebagai pengawas/petugas gudang, saya ingin menelusuri dan mencocokkan stok. | Kartu stok menampilkan mutasi dan saldo berjalan, dengan saldo akhir = stok barang; opname menghitung selisih dan stok berubah hanya setelah disetujui. |
| US-09 | Sebagai manajer, saya ingin membatalkan transaksi yang salah tanpa menghapus riwayat. | Alasan wajib; stok, kuota, dan hutang dipulihkan; void yang melanggar A-13 ditolak. |
| US-10 | Sebagai pengurus, saya ingin rekap bulanan yang dapat dicetak atau diekspor. | Filter jenis dan periode tersedia; total cocok dengan transaksi; file ekspor dapat dibuka di Excel. |
| US-11 | Sebagai pengelola gerai, saya ingin aplikasi tetap berjalan tanpa internet dan memberi pesan jelas bila ada masalah. | Fitur inti berjalan dengan database lokal; bila database mati, muncul pesan dengan langkah perbaikan, bukan crash. |
| US-12 | Sebagai pengguna, saya ingin memilih tema yang nyaman. | Tema berlaku di seluruh komponen, tetap terbaca, dan tersimpan per pengguna. |

---

## 9. Alur Pengguna Utama

**Harian gerai:** login → cek dashboard (stok kritis, kuota) → petugas gudang mencatat barang datang → kasir melayani penjualan dan penyaluran → retur/barang rusak dicatat bila ada → kasir menutup hari. Secara berkala dilakukan stok opname; di akhir bulan, rekap diekspor untuk pengurus.

**Penerimaan:** pilih sumber → pilih pemasok/penitip dan isi nomor surat jalan → tambah item (scan/cari, jumlah, harga beli, tanggal kedaluwarsa bila ada) → validasi → simpan secara atomik → notifikasi; stok, HPP, dashboard, dan log diperbarui.

**Penjualan:** mode Penjualan → pilih Umum atau Anggota → scan barang (harga otomatis) → sistem menolak jumlah melebihi stok → input uang diterima → simpan (stok berkurang, mutasi tercatat, hutang penitip dibuat bila ada barang titipan) → cetak struk.

**Penyaluran subsidi:** mode Penyaluran → cari penerima lewat NIK/nama → sistem menampilkan status dan sisa kuota per barang → pilih barang dan jumlah → sistem memeriksa status, HET, kuota, dan stok → simpan (stok dan kuota diperbarui secara atomik) → cetak bukti.

**Konsinyasi:** daftarkan penitip → buat barang titipan dengan harga titip → terima barang lewat sumber KONSINYASI → barang terjual sehingga hutang otomatis terbentuk → bendahara mencatat pelunasan (FIFO) → barang yang tidak laku diretur ke penitip.

**Stok opname:** buat sesi (semua barang atau per kategori) → sistem menyimpan salinan stok sistem → input hitung fisik → selisih dihitung → ajukan dengan alasan → Manajer/Pengurus lain menyetujui atau menolak → bila disetujui, mutasi PENYESUAIAN dibuat.

Alur di atas digambarkan sebagai activity diagram pada dokumen perancangan (bagian 15).

---

## 10. Kebutuhan UI/UX dan Design System

### 10.1 Prinsip Desain

- **Untuk pengguna awam:** bahasa sehari-hari, satu layar satu tugas, tombol besar dengan ikon dan teks, dialog konfirmasi untuk aksi berisiko, dan pesan error yang menyebut penyebab sekaligus langkah perbaikan (mis. "Jumlah melebihi sisa kuota Bu Siti: sisa 2 tabung").
- **Konsistensi:** margin, padding, radius, ikon, warna, dan ukuran kontrol didefinisikan di ResourceDictionary bersama.
- **Aksesibilitas:** kontras teks ≥ 4,5:1, fokus keyboard terlihat, dan status tidak hanya dibedakan lewat warna.
- **Efisiensi input:** tab-order yang rapi; pintasan F2 (kasir), Ctrl+N (baru), Ctrl+S (simpan), Ctrl+F (cari), Esc (batal); fokus otomatis ke kolom barcode.
- **Ukuran minimum** 1280×720; tabel dan panel tidak saling menutupi.
- **State UI** lengkap: loading, sukses, error, kosong, akses ditolak, dan database tidak terhubung.

### 10.2 Kartu Statistik Dashboard

| Kartu | Isi | Sumber data |
| --- | --- | --- |
| Penjualan Hari Ini | Total rupiah dan jumlah transaksi | SUM/COUNT transaksi aktif hari ini |
| Nilai Persediaan | Nilai stok milik koperasi | Σ stok × HPP rata-rata (MILIK) |
| Stok Kritis | Jumlah barang Menipis + Habis | Barang aktif dengan stok ≤ minimum |
| Hutang Penitip | Total sisa hutang | Σ (NilaiHutang − JumlahDibayar) |
| Subsidi Bulan Ini | Total tersalur vs total kuota | Σ PemakaianKuota vs kuota periode berjalan |
| Perkiraan Kedaluwarsa | Jumlah barang dalam ambang | Estimasi A-09 |

### 10.3 Kebutuhan Rich UI

Bagian ini memetakan fitur Rich UI yang akan didemonstrasikan. Sesuaikan dengan rubrik penilaian dosen.

| Fitur Rich UI | Penerapan di STOKARA |
| --- | --- |
| Data binding MVVM | Semua View terikat ke ViewModel; tidak ada logika bisnis di code-behind. |
| Validasi inline | INotifyDataErrorInfo dengan pesan di bawah field dan border merah pada form barang, penerimaan, dan mitra. |
| Custom control dan template | Kartu statistik (UserControl), badge status, DataGrid dengan DataTemplate/CellTemplate, tombol bergaya bersama. |
| Visualisasi data | Grafik garis (penjualan), batang (terlaris), dan progress/donat (kuota) memakai LiveCharts2 atau OxyPlot. |
| Tema dinamis | Pertukaran ResourceDictionary Light/Dark saat runtime memakai DynamicResource. |
| Animasi dan transisi | Transisi halaman (fade/slide), animasi angka pada kartu statistik, toast yang muncul dan menghilang. |
| Interaksi kaya | Pencarian dengan saran (autocomplete), dialog modal, panel detail yang dapat dibuka-tutup, pintasan keyboard. |
| Responsivitas | Operasi database memakai async/await dengan indikator loading; virtualisasi pada daftar besar. |

### 10.4 Palet Warna dan Design Token - "Bata dan Padi"

Palet ini terinspirasi tanah, padi, dan daun desa. Merah bata dipilih menggantikan merah murni agar tetap dekat dengan identitas Merah Putih tetapi nyaman dilihat lama. Nilai hex adalah usulan awal dan perlu diuji kontrasnya.

| Token | Light | Dark | Dipakai untuk |
| --- | --- | --- | --- |
| BrushPrimary / Hover | #A8442B / #8C3520 | #E0785A / #EA8C71 | Tombol utama, menu aktif |
| BrushOnPrimary | #FFFFFF | #1B1612 | Teks di atas warna utama |
| BrushAccent / OnAccent | #C9962B / #2B2118 | #E3B04B / #1B1612 | Aksen, ikon, seri kedua grafik |
| BrushSecondary | #4F6B4A | #8FB287 | Elemen pendukung, seri ketiga grafik |
| BrushBackground | #F6F0E4 | #1B1612 | Latar halaman |
| BrushSurface / SurfaceAlt | #FFFBF2 / #F1E9D8 | #261F19 / #2E261F | Kartu, tabel, baris selang-seling |
| BrushBorder | #E3D8C3 | #3A3027 | Garis pemisah |
| BrushTextPrimary / Secondary | #2B2118 / #6B5B4B | #F1E8D8 / #B9AB97 | Teks utama dan pendukung |

| Badge | Latar (light) | Teks (light) | Latar (dark) | Teks (dark) |
| --- | --- | --- | --- | --- |
| Aman | #E4EEDD | #2F5A2B | #243022 | #8FBF86 |
| Menipis | #F8E7C6 | #7A4A06 | #3A2D17 | #E3A850 |
| Habis / Ditolak | #F6D9D5 | #8E1B15 | #3C1F1D | #F0847B |
| SUBSIDI | #D6ECEA | #14524F | #17312F | #5CC2BB |
| TITIPAN | #E4DFF0 | #43366B | #2B2540 | #B3A5E0 |

**Aturan pemakaian:**
- Satu tombol Primary per layar.
- Accent tidak dipakai dengan teks putih.
- Merah hanya untuk kondisi bermasalah.
- Warna solid status dipakai sebagai isian atau ikon, bukan sebagai warna teks di atas latar terang.
- Urutan warna grafik: Primary, Accent, Secondary, SUBSIDI.

---

## 11. Kebutuhan Nonfungsional

| ID | Kategori | Persyaratan |
| --- | --- | --- |
| NFR-01 | Performa | UI tidak membeku: operasi database memakai async/await; daftar besar memakai pagination atau virtualisasi. Pencarian ≤ 2 detik pada ±5.000 barang dan ±50.000 transaksi (didukung indeks). |
| NFR-02 | Offline-first | Seluruh fitur inti berjalan tanpa internet menggunakan database lokal. |
| NFR-03 | Integritas | Primary key, foreign key, constraint CHECK, dan transaksi database untuk setiap operasi multi-tabel. |
| NFR-04 | Konkurensi | Pengurangan stok dan kuota memakai UPDATE atomik bersyarat, sehingga dua sesi bersamaan tidak membuat stok negatif atau kuota terlampaui. |
| NFR-05 | Keamanan | Query berparameter; password di-hash dengan salt (PBKDF2/bcrypt); connection string di file konfigurasi, bukan di kode; akun database dengan hak minimum. |
| NFR-06 | Privasi | NIK hanya tampil bagi peran yang berwenang dan disamarkan di tampilan umum (mis. 3515••••••••0012); NIK dan password tidak pernah ditulis ke log. |
| NFR-07 | Keandalan | Error database ditangkap dan dijelaskan dengan pesan yang dapat ditindaklanjuti; error teknis dicatat ke file log. |
| NFR-08 | Pemeliharaan | Lapisan View, ViewModel, Model, Repository, dan Service terpisah; logika bisnis dapat diuji dengan unit test. |
| NFR-09 | Kompatibilitas | Windows 10/11 dan versi .NET LTS yang dipilih. |
| NFR-10 | Lokalisasi | Antarmuka berbahasa Indonesia; format tanggal dd/MM/yyyy dan mata uang Rupiah. |

---

## 12. Rancangan Data

| Entitas | Atribut inti | Catatan |
| --- | --- | --- |
| Users | UserId, Username, PasswordHash, Nama, Role, Tema, IsActive, CreatedAt, LastLoginAt | Role: MANAJER/KASIR/GUDANG/PENGURUS. |
| Kategori, Satuan | Id, Nama, IsActive | Nama unik. |
| Pemasok | PemasokId, Kode, Nama, Jenis (BUMN/BUMD/LOKAL), Telepon, Alamat, IsActive | |
| Penitip | PenitipId, Nama, Telepon, Alamat, IsActive | Tanpa komisi persen (A-05). |
| Anggota | AnggotaId, NomorAnggota, NIK, Nama, Alamat, Dusun, Telepon, Status, TanggalBergabung | NomorAnggota dan NIK unik. |
| PenerimaSubsidi | PenerimaId, NIK, Nama, JenisPenerima, Dusun, AnggotaId (opsional), Status | NIK unik (A-11). |
| Barang | BarangId, SKU, Barcode, Nama, KategoriId, SatuanId, Kepemilikan (MILIK/TITIPAN), PenitipId, HargaTitip, HargaAnggota, HargaUmum, HargaPokokRata, IsSubsidi, HET, StokMinimum, StokSaatIni, IsActive | Lihat constraint 12.3. |
| AturanKuota | AturanId, BarangId, JenisPenerima, Periode, JumlahKuota | Unik (BarangId, JenisPenerima, Periode). |
| PemakaianKuota | PemakaianId, PenerimaId, BarangId, Periode, JumlahTerpakai | Unik (PenerimaId, BarangId, Periode). |
| Penerimaan / PenerimaanDetail | NomorDokumen, Tanggal, Sumber (STOK_AWAL/PEMBELIAN/SUBSIDI/KONSINYASI), PemasokId, PenitipId, NomorSuratJalan, Status, UserId; BarangId, Jumlah, HargaBeli, TanggalKedaluwarsa | Status: AKTIF/DIBATALKAN. |
| Penjualan / PenjualanDetail | NomorDokumen, Tanggal, Jenis (PENJUALAN/PENYALURAN_SUBSIDI), JenisPelanggan (UMUM/ANGGOTA), AnggotaId, PenerimaId, Total, UangDiterima, Status, UserId; BarangId, Jumlah, HargaSatuan, HargaPokok | HargaPokok berupa salinan (A-08). |
| HutangPenitip | HutangId, PenitipId, PenjualanDetailId, Jumlah, HargaTitip, NilaiHutang, Komisi, JumlahDibayar, Status | Dibuat otomatis (FR-24). |
| PelunasanPenitip | PelunasanId, PenitipId, Tanggal, JumlahBayar, Catatan, UserId | Dialokasikan FIFO ke HutangPenitip. |
| PengeluaranLain / Detail | NomorDokumen, Tanggal, Jenis (RETUR_PEMASOK/RETUR_PENITIP/RUSAK/KEDALUWARSA/HILANG), Alasan, UserId; BarangId, Jumlah | Alasan wajib. |
| MutasiStok | MutasiId, BarangId, Tanggal, JenisMutasi, RefTipe, RefId, Masuk, Keluar, SaldoSetelah, UserId | Hanya ditambah, tidak pernah diubah. |
| StokOpname / Detail | NomorDokumen, Tanggal, Status, DiajukanOleh, DisetujuiOleh; BarangId, StokSistem, StokFisik, Selisih, Alasan | Selisih = fisik − sistem. |
| TutupHari | Tanggal, JumlahTransaksi, TotalPenjualan, ModalAwal, UangSeharusnya, UangFisik, Selisih, UserId | Satu baris per hari (A-17). |
| Aktivitas | AktivitasId, UserId, JenisAksi, Entitas, EntitasId, Keterangan, Waktu | Tanpa NIK atau password. |
| Pengaturan | Kunci, Nilai | Ambang kedaluwarsa, periode kuota, profil gerai. |

### 12.1 Relasi Utama

```
Kategori 1─* Barang *─1 Satuan          Penitip 1─* Barang (TITIPAN)
Pemasok/Penitip 1─* Penerimaan 1─* PenerimaanDetail *─1 Barang
Anggota 1─* Penjualan ; PenerimaSubsidi 1─* Penjualan 1─* PenjualanDetail *─1 Barang
Barang 1─* AturanKuota ; PenerimaSubsidi 1─* PemakaianKuota *─1 Barang
PenjualanDetail 1─0..1 HutangPenitip *─1 Penitip 1─* PelunasanPenitip
Barang 1─* MutasiStok ; StokOpname 1─* StokOpnameDetail *─1 Barang
Users 1─* (Penerimaan, Penjualan, PengeluaranLain, StokOpname, MutasiStok, Aktivitas)
```

### 12.2 Jenis Mutasi Stok

| Jenis | Efek | Dokumen |
| --- | --- | --- |
| MASUK_STOK_AWAL / MASUK_PEMBELIAN / MASUK_SUBSIDI / MASUK_KONSINYASI | + | Penerimaan |
| KELUAR_JUAL / KELUAR_SUBSIDI | − | Penjualan / Penyaluran |
| KELUAR_RETUR / KELUAR_RUSAK | − | PengeluaranLain |
| VOID | Membalik mutasi dokumen | Void |
| PENYESUAIAN | ± selisih | Opname disetujui |

### 12.3 Constraint dan Indeks

- **UNIQUE:** SKU, Barcode, NomorAnggota, NIK (Anggota, PenerimaSubsidi), Username, NomorDokumen, (BarangId, JenisPenerima, Periode) pada AturanKuota, (PenerimaId, BarangId, Periode) pada PemakaianKuota.
- **CHECK:**
  - StokSaatIni ≥ 0; Detail.Jumlah > 0; harga ≥ 0.
  - `IsSubsidi = 0 OR (HET > 0 AND HargaUmum <= HET AND HargaAnggota <= HET)`.
  - `Kepemilikan = 'MILIK' OR (PenitipId IS NOT NULL AND HargaTitip <= HargaUmum)`.
  - JumlahDibayar ≤ NilaiHutang.
- **Indeks:** Barang.Nama, MutasiStok(BarangId, Tanggal), Penjualan(Tanggal), PemakaianKuota(PenerimaId, Periode).
- **Penghapusan:** ON DELETE NO ACTION; master data memakai soft delete (IsActive).

---

## 13. Laporan

| Laporan | Isi | Filter |
| --- | --- | --- |
| Penjualan & Laba Kotor | Transaksi, total, produk terlaris, laba kotor = penjualan − HPP − hak penitip | Periode, jenis pelanggan, kategori |
| Stok & Nilai Persediaan | Stok, status (Aman/Menipis/Habis), perkiraan kedaluwarsa, nilai persediaan milik koperasi | Kategori, jenis, status, tanggal posisi |
| Penyaluran Subsidi | Tersalur per penerima/barang/dusun, kuota terpakai vs tersedia | Periode, barang, dusun |
| Konsinyasi | Stok titipan, penjualan titipan, hutang, pelunasan per penitip | Periode, penitip |
| Mutasi Stok | Seluruh mutasi, termasuk penerimaan, retur, dan penyesuaian opname | Periode, barang, jenis |
| Rekap Bulanan | Gabungan ringkasan kelima laporan di atas plus rekap tutup kasir, siap cetak/ekspor | Bulan |

Laba kotor bukan pengganti akuntansi koperasi. SHU dan akuntansi lengkap berada di luar lingkup.

---

## 14. Arsitektur Teknis

| Lapisan | Tanggung jawab | Contoh |
| --- | --- | --- |
| View | XAML, presentasi | ShellWindow, DashboardView, KasirView, BarangView |
| ViewModel | State UI, command, validasi tampilan | KasirViewModel, BarangFormViewModel |
| Model | Entitas domain | Barang, Penjualan, AturanKuota, HutangPenitip |
| Repository | Query berparameter dan pemetaan | BarangRepository, PenjualanRepository |
| Service | Aturan bisnis, otorisasi, transaksi | AuthService, PenjualanService, SubsidiService, KonsinyasiService, StokService, OpnameService, LaporanService |
| Infrastruktur | Koneksi, log, tema, navigasi, dialog, cetak, ekspor | DbConnectionFactory, ThemeService, NavigationService, PrintService, ExportService |

### 14.1 Prinsip Transaksi

Penyimpanan penjualan berjalan dalam satu `SqlTransaction` dengan urutan berikut:

1. Simpan header dan detail.
2. Kurangi stok dengan `UPDATE Barang SET StokSaatIni = StokSaatIni - @jml WHERE BarangId = @id AND StokSaatIni >= @jml`.
3. Untuk penyaluran subsidi, perbarui PemakaianKuota dengan syarat `JumlahTerpakai + @jml <= @kuota`.
4. Buat MutasiStok.
5. Untuk barang titipan, buat HutangPenitip.

Bila langkah 2 atau 3 tidak mengubah satu baris pun, seluruh transaksi di-rollback dan pengguna mendapat pesan yang jelas.

### 14.2 Struktur Folder

```
Stokara/
├── Models/  Views/  ViewModels/  Repositories/  Services/
├── Infrastructure/ (Db, Navigation, Theme, Print, Export, Logging)
├── Helpers/ (Converter, Validator, RelayCommand)
├── Resources/ (Themes/Light.xaml, Themes/Dark.xaml, Styles, Icons)
├── Database/ (schema.sql, seed.sql)
├── Stokara.Tests/ (unit test service)
└── appsettings.json
```

---

## 15. Rencana Implementasi dan Dokumentasi

Setiap fase menghasilkan bukti yang langsung dipakai di laporan dokumentasi perancangan dan implementasi.

| Fase | Aktivitas | Deliverable | Bukti untuk laporan |
| --- | --- | --- | --- |
| 0 - Perancangan | Wawancara narasumber, revisi asumsi, use case diagram, ERD, activity diagram (bagian 9), sequence diagram penjualan/penyaluran, wireframe 4 layar utama (Dashboard, Kasir, Barang, Penerimaan) | PRD final, dokumen perancangan | Hasil wawancara, semua diagram, wireframe |
| 1 - Fondasi & Database | Project WPF, MVVM, shell, navigasi, tema; schema, constraint, seed | Shell aplikasi, STOKARA_DB | Screenshot shell dan tema, skrip SQL |
| 2 - Master Data & Akses | Login, peran, pengguna, barang, mitra, aturan kuota | Modul master berjalan | Screenshot form dan validasi inline |
| 3 - Stok & Transaksi | Penerimaan, pengeluaran, kartu stok, kasir (2 mode), struk, riwayat | Alur inti berjalan | Potongan kode transaksi atomik, screenshot kasir |
| 4 - Konsinyasi, Dashboard, Laporan | Hutang & pelunasan, kartu statistik, grafik, 6 laporan, ekspor | Dashboard dinamis | Screenshot dashboard dan laporan |
| 5 - Should, QA & Rilis | Opname, void, tutup kasir, backup, panduan; pengujian (bagian 16), uji pengguna, perbaikan | Build rilis, panduan pengguna | Tabel hasil uji, catatan uji pengguna |

---

## 16. Strategi Pengujian

| Area | Skenario minimum | Hasil yang diharapkan |
| --- | --- | --- |
| Akses | Login benar/salah/akun nonaktif; kasir mencoba mengubah HET; gudang mencoba mengubah harga | Akses sesuai peran; ditolak di service dan dicatat di log |
| Master & validasi | Field kosong, duplikat SKU/NIK, harga negatif, barang subsidi tanpa HET atau dengan harga > HET | Input tidak valid ditolak dengan pesan di form |
| Stok | Penerimaan satu/banyak item dari semua sumber; pengeluaran; kartu stok vs stok barang | Stok, HPP, dan mutasi benar; saldo kartu stok = stok |
| Penjualan | Jumlah <, =, > stok; pelanggan umum vs anggota | Kelebihan ditolak; harga sesuai jenis pelanggan |
| Subsidi | Harga > HET, penerima tidak terdaftar/nonaktif, kuota habis, sisa kuota pas | Penolakan benar; kuota terpakai akurat |
| Konsinyasi | Jual barang titipan; pelunasan sebagian, penuh, dan berlebih | Hutang dan alokasi FIFO benar; pembayaran berlebih ditolak |
| Atomisitas & konkurensi | Error disimulasikan di tengah penyimpanan; dua sesi memakai stok/kuota yang sama | Tidak ada perubahan sebagian; stok tidak negatif; kuota tidak terlampaui |
| Opname, void, tutup kasir | Opname disetujui/ditolak; void sesuai dan melanggar A-13; transaksi setelah hari ditutup | Sesuai aturan |
| Sistem | Database mati; backup lalu restore; ganti tema; data besar | Tidak crash; data pulih utuh; tampilan terbaca; tetap responsif |
| Usability | Uji dengan pengelola gerai nyata | ≥ 80% tugas selesai tanpa bantuan berarti |

### 16.1 Contoh Kasus Uji

| ID | Skenario | Langkah singkat | Hasil yang diharapkan |
| --- | --- | --- | --- |
| TC-01 | Jual melebihi stok | Stok 5, jual 8 | Ditolak: stok tidak cukup |
| TC-02 | Simpan barang subsidi di atas HET | HET 18.000, harga 20.000 di form barang | Ditolak saat menyimpan barang |
| TC-03 | HET diturunkan | Harga 18.000, HET diubah menjadi 17.000, lalu salurkan | Penyaluran ditolak sampai harga disesuaikan |
| TC-04 | Kuota habis | Kuota 4, terpakai 4, salurkan 1 | Ditolak: kuota habis |
| TC-05 | Penerima tidak terdaftar | Cari NIK yang tidak ada | Penyaluran tidak dapat dilanjutkan |
| TC-06 | Campur subsidi di mode Penjualan | Scan barang subsidi di mode Penjualan | Ditolak dengan arahan ke mode Penyaluran |
| TC-07 | Jual barang titipan | Jual 3, harga titip 10.000, harga jual 12.000 | Hutang +30.000, komisi 6.000, stok −3 |
| TC-08 | Pelunasan FIFO | Hutang 20.000 (lama) dan 30.000 (baru), bayar 25.000 | Hutang lama LUNAS, hutang baru SEBAGIAN (dibayar 5.000) |
| TC-09 | Pelunasan berlebih | Sisa hutang 30.000, bayar 40.000 | Ditolak |
| TC-10 | HPP rata-rata | Stok 10 @1.000, terima 10 @1.200 | HPP menjadi 1.100 |
| TC-11 | Void penyaluran subsidi | Void transaksi hari ini | Stok kembali, kuota dipulihkan |
| TC-12 | Void titipan yang sudah dibayar | Hutang terkait sudah dilunasi sebagian | Void ditolak |
| TC-13 | Opname disetujui | Fisik 22, sistem 25, disetujui pengguna lain | Stok jadi 22, mutasi PENYESUAIAN −3 |
| TC-14 | Database mati | Hentikan SQL Server, buka aplikasi | Pesan jelas berisi langkah perbaikan, tidak crash |

---

## 17. Matriks Ketertelusuran

| Sasaran | Kebutuhan | Pengujian |
| --- | --- | --- |
| Stok akurat | FR-13-16, BR-01, BR-02, BR-14, BR-17 | Stok, atomisitas, TC-01, TC-10, TC-13 |
| Kepatuhan subsidi | FR-12, FR-18, BR-07-09, A-02-04, A-10 | Subsidi, TC-02-06, TC-11 |
| Konsinyasi tertib | FR-24-26, BR-11-13, A-05-07 | Konsinyasi, TC-07-09, TC-12 |
| Kemudahan penggunaan | FR-17, FR-30, FR-33, bagian 10 | Usability |
| Ketertelusuran | FR-15, FR-29 | Kartu stok, akses |
| Keandalan offline | FR-31, FR-33, NFR-02, NFR-07 | Sistem, TC-14 |
| Keamanan | FR-01-03, NFR-05, NFR-06, A-16 | Akses |
| Pelaporan | FR-04-07, FR-27-28 | Dashboard, laporan |

---

## 18. Risiko dan Mitigasi

| Risiko | Mitigasi |
| --- | --- |
| Pencatatan ganda dengan Simkopdes membuat pengurus enggan memakai | Kasir cepat, rekap ekspor, klarifikasi peran aplikasi lewat narasumber |
| Asumsi (bagian 3.3) tidak sesuai kondisi nyata | Wawancara di Fase 0; skema kuota dan konsinyasi dibuat terpisah agar mudah diubah |
| Aturan subsidi berubah | HET, kuota, dan periode menjadi parameter yang tercatat di log |
| Literasi digital pengguna rendah | UI sederhana, bahasa awam, panduan singkat, uji dengan pengguna nyata |
| Race condition stok/kuota | UPDATE atomik bersyarat dan uji konkurensi |
| Logika bisnis bocor ke code-behind atau otorisasi hanya di UI | Code review; aturan dan otorisasi di service dengan unit test |
| Data NIK bocor atau data hilang | NIK disamarkan dan tidak masuk log; backup ke media terpisah dan restore diuji |
| Cakupan tetap terlalu luas | Kerjakan seluruh Must terlebih dahulu; fitur Should hanya di Fase 5 |

---

## 19. Hal yang Diverifikasi Lewat Wawancara

Keputusan desain sudah ditetapkan di bagian 3.3. Wawancara digunakan untuk **memvalidasi** asumsi tersebut, terutama:

1. Aturan kuota riil (A-03, A-04): apakah ditentukan per jenis penerima dan per bulan?
2. Skema konsinyasi riil (A-05) dan tempo pembayaran ke penitip.
3. Apakah penerima subsidi harus anggota (A-11).
4. Format rekap yang diminta pengurus atau dinas (A-18).
5. Perangkat gerai (komputer, printer struk, barcode scanner) dan kondisi internet.

Bila hasil wawancara berbeda, perbarui asumsi terkait dan catat di Riwayat Revisi.

---

## 20. Definisi Selesai (Definition of Done)

- Semua kebutuhan Must diimplementasikan dan kasus uji Must lulus 100%.
- Pelanggaran HET, status penerima, dan kuota ditolak oleh service, bukan hanya oleh UI.
- Stok tidak dapat bernilai negatif, seluruh mutasi tercatat, dan saldo kartu stok = stok barang.
- Statistik dan grafik bersumber dari database.
- Aplikasi berjalan tanpa internet dan tidak crash saat database mati.
- Query berparameter, password di-hash, dan akses divalidasi di service.
- UI konsisten dengan design system, mendukung kedua tema, dan fitur Rich UI pada bagian 10.3 terdemonstrasi.
- Aplikasi dicoba oleh minimal satu pengelola Kopdes dan hasilnya didokumentasikan.
- Kode, skrip database, panduan instalasi, panduan pengguna, dokumen perancangan, dan hasil pengujian lengkap.

---

## Lampiran A - Persetujuan PRD

| Peran | Nama | Tanggal | Status |
| --- | --- | --- | --- |
| Pemilik Produk (Pengurus/Manajer Kopdes) | \_\_\_\_\_\_\_\_\_\_ | \_\_\_\_\_\_\_\_\_\_ | Belum disetujui |
| Dosen / Penilai | \_\_\_\_\_\_\_\_\_\_ | \_\_\_\_\_\_\_\_\_\_ | Belum disetujui |
| Perwakilan Pengguna (Kasir/Pengelola) | \_\_\_\_\_\_\_\_\_\_ | \_\_\_\_\_\_\_\_\_\_ | Belum disetujui |

## Lampiran B - Data Narasumber Real Case

| Item | Isian |
| --- | --- |
| Nama Kopdes; desa, kecamatan, kabupaten | \[isi\] |
| Status badan hukum dan tanggal mulai operasional | \[isi\] |
| Jumlah anggota (perkiraan) dan unit usaha yang berjalan | \[isi\] |
| Barang yang dijual (bebas, subsidi, titipan) dan perkiraan jumlah SKU | \[isi\] |
| Cara pencatatan saat ini dan pemakaian Simkopdes | \[isi\] |
| Kondisi internet dan perangkat tersedia | \[isi\] |
| Masalah utama menurut narasumber | \[isi\] |
| Nama narasumber, jabatan, tanggal wawancara | \[isi\] |

## Lampiran C - Daftar Pertanyaan Wawancara

1. Unit usaha apa yang sudah berjalan, dan barang apa yang paling sering dijual?
2. Bagaimana barang subsidi diterima dan disalurkan? Siapa yang berhak, dan bagaimana kuotanya ditentukan dan dicek (per orang, per jenis penerima, per bulan)?
3. Apakah penerima subsidi harus anggota koperasi?
4. Bagaimana harga ditetapkan untuk anggota dan bukan anggota?
5. Apakah ada barang titipan? Apakah kesepakatannya berupa harga titip tetap atau komisi, dan kapan penitip dibayar?
6. Di mana stok dan penjualan dicatat saat ini, dan siapa yang mencatat?
7. Bagian Simkopdes mana yang sudah dipakai, dan mana yang menyulitkan?
8. Bagaimana kondisi internet di gerai, dan perangkat apa saja yang tersedia?
9. Seberapa sering terjadi selisih stok atau barang habis tanpa disadari?
10. Laporan apa yang diminta pengurus atau dinas, dan seberapa sering?

## Lampiran D - Sumber Rujukan

| Topik | Sumber |
| --- | --- |
| Jumlah koperasi dan akun Simkopdes (20 Juli 2026) | [Siaran pers Kemenko Pangan](https://kemenkopangan.go.id/assets/images/artikel/f7c2a9f42a1943e3f02c73c2c8c2e29e.pdf) |
| Operasional serentak 1 Oktober 2026 dan barang yang disalurkan | [RCTI+/Okezone](https://www.rctiplus.com/news/detail/okezone/5483154/kopdes-merah-putih-beroperasi-1-oktober--ini-daftar-barang-yang-dijual) |
| Kopdes sebagai penyalur subsidi/bansos | [IDN Financials](https://www.idnfinancials.com/id/news/66153/prabowo-backs-kopdes-for-aid-distribution-and-farm-collection) |
| Kopdes sebagai sub-pangkalan LPG 3 kg | [CNBC Indonesia](https://www.cnbcindonesia.com/news/20250723162208-4-651974/lpg-3-kg-akan-dijual-di-semua-koperasi-merah-putih-ini-kata-esdm) |
| Konsinyasi pada gerai Kopdes Melawai | [Katadata](https://katadata.co.id/amp/foto/68f1f1c18dd5e/foto-koperasi-merah-putih-melawai-hadirkan-sembako-dengan-harga-terjangkau/) |
| Simkopdes sebagai dashboard stok, transaksi, dan distribusi subsidi | [GoodStats](https://goodstats.id/article/produk-kopdes-merah-putih-yang-paling-banyak-dibeli-masyarakat-7YSs5) |
| Aplikasi mobile Simkopdes | [App Store](https://apps.apple.com/id/app/simkopdes/id6749216676) |
| Literasi digital pengurus sebagai hambatan | [Jurnal Bernas UNMA](https://www.ejournal.unma.ac.id/index.php/bernas/article/view/15622/7246) |

*Angka dan kebijakan pada sumber di atas dapat berubah; periksa kembali tautan sebelum dokumen final dikumpulkan.*
