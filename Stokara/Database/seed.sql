/* =====================================================================
   STOKARA - Data awal (master data contoh)
   Dijalankan otomatis oleh aplikasi setelah schema.sql.
   Akun pengguna dibuat oleh aplikasi (password perlu di-hash di C#).
   Stok awal, penyaluran, dan penjualan contoh dibuat oleh DemoDataSeeder
   melalui service supaya mutasi, HPP, kuota, dan hutang tetap konsisten.
   ===================================================================== */

INSERT INTO dbo.Pengaturan (Kunci, Nilai) VALUES
 (N'NamaGerai',            N'Gerai Kopdes Merah Putih'),
 (N'AlamatGerai',          N'[Isi alamat desa]'),
 (N'TeleponGerai',         N'-'),
 (N'AmbangKedaluwarsaHari', N'30'),
 (N'PeriodeKuota',         N'BULANAN'),
 (N'UkuranHalaman',        N'25');
GO

INSERT INTO dbo.Kategori (Nama) VALUES
 (N'Sembako'), (N'Pupuk & Pertanian'), (N'Gas & Energi'), (N'Minuman'),
 (N'Makanan Ringan'), (N'Kebutuhan Rumah Tangga'), (N'Produk UMKM');
GO

INSERT INTO dbo.Satuan (Nama) VALUES
 (N'pcs'), (N'kg'), (N'liter'), (N'sak'), (N'tabung'), (N'bungkus'), (N'botol'), (N'karung');
GO

INSERT INTO dbo.Pemasok (Kode, Nama, Jenis, Telepon, Alamat) VALUES
 (N'BULOG',   N'Perum BULOG Kanwil',          N'BUMN',  N'031-000001', N'Kantor Wilayah'),
 (N'PUPUK',   N'Distributor Pupuk Indonesia', N'BUMN',  N'031-000002', N'Gudang Kecamatan'),
 (N'LPG',     N'Agen LPG 3 kg Resmi',         N'BUMN',  N'031-000003', N'Kecamatan'),
 (N'GROSIR',  N'Toko Grosir Sumber Rejeki',   N'LOKAL', N'0812000004', N'Pasar Kecamatan');
GO

INSERT INTO dbo.Penitip (Nama, Telepon, Alamat) VALUES
 (N'UMKM Keripik Bu Sri', N'0812111111', N'Dusun Krajan'),
 (N'Madu Hutan Pak Darto', N'0812222222', N'Dusun Sumberan');
GO

INSERT INTO dbo.Anggota (NomorAnggota, NIK, Nama, Alamat, Dusun, Telepon, TanggalBergabung) VALUES
 (N'AGT-0001', '3578010101800001', N'Siti Aminah',   N'RT 01 RW 02', N'Krajan',   N'0813000001', '2026-07-21'),
 (N'AGT-0002', '3578010202850002', N'Budi Santoso',  N'RT 03 RW 01', N'Sumberan', N'0813000002', '2026-07-21'),
 (N'AGT-0003', '3578010303900003', N'Rina Wulandari',N'RT 02 RW 03', N'Krajan',   N'0813000003', '2026-08-02');
GO

INSERT INTO dbo.PenerimaSubsidi (NIK, Nama, JenisPenerima, Dusun, AnggotaId) VALUES
 ('3578010101800001', N'Siti Aminah',   N'RUMAH_TANGGA', N'Krajan',   1),
 ('3578010202850002', N'Budi Santoso',  N'PETANI',       N'Sumberan', 2),
 ('3578010404750004', N'Sutrisno',      N'PETANI',       N'Sumberan', NULL),
 ('3578010505880005', N'Warung Bu Yati',N'USAHA_MIKRO',  N'Krajan',   NULL);
GO

/* Barang: stok selalu 0 di sini; stok masuk lewat penerimaan (A-14). */
INSERT INTO dbo.Barang (SKU, Barcode, Nama, KategoriId, SatuanId, Kepemilikan, PenitipId, HargaTitip,
                        HargaAnggota, HargaUmum, IsSubsidi, HET, StokMinimum) VALUES
 (N'SBK-001', N'8991000000011', N'Beras Premium 5 kg',          1, 6, N'MILIK', NULL, NULL, 72000, 75000, 0, NULL, 10),
 (N'SBK-002', N'8991000000028', N'Beras SPHP 5 kg',             1, 6, N'MILIK', NULL, NULL, 62500, 62500, 1, 62500, 10),
 (N'SBK-003', N'8991000000035', N'Minyak Goreng 1 L',           1, 7, N'MILIK', NULL, NULL, 17500, 18000, 0, NULL, 12),
 (N'SBK-004', N'8991000000042', N'Gula Pasir 1 kg',             1, 2, N'MILIK', NULL, NULL, 17000, 17500, 0, NULL, 10),
 (N'SBK-005', N'8991000000059', N'Telur Ayam',                  1, 2, N'MILIK', NULL, NULL, 28000, 29000, 0, NULL, 5),
 (N'PPK-001', N'8991000000066', N'Pupuk Urea Bersubsidi 50 kg', 2, 4, N'MILIK', NULL, NULL, 112500, 112500, 1, 112500, 5),
 (N'PPK-002', N'8991000000073', N'Pupuk NPK Bersubsidi 50 kg',  2, 4, N'MILIK', NULL, NULL, 115000, 115000, 1, 115000, 5),
 (N'GAS-001', N'8991000000080', N'LPG 3 kg',                    3, 5, N'MILIK', NULL, NULL, 18000, 18000, 1, 19000, 15),
 (N'MNM-001', N'8991000000097', N'Air Mineral 600 ml',          4, 7, N'MILIK', NULL, NULL, 3500, 4000, 0, NULL, 24),
 (N'RT-001',  N'8991000000103', N'Sabun Cuci Piring 800 ml',    6, 7, N'MILIK', NULL, NULL, 14000, 15000, 0, NULL, 6),
 (N'UMK-001', N'8991000000110', N'Keripik Singkong Bu Sri',     7, 6, N'TITIPAN', 1, 8000, 10000, 10000, 0, NULL, 5),
 (N'UMK-002', N'8991000000127', N'Madu Hutan 250 ml',           7, 7, N'TITIPAN', 2, 45000, 55000, 55000, 0, NULL, 3);
GO

/* Aturan kuota periode berjalan (bulanan) - A-03, A-04 */
DECLARE @p NVARCHAR(7) = FORMAT(GETDATE(), 'yyyy-MM');
INSERT INTO dbo.AturanKuota (BarangId, JenisPenerima, Periode, JumlahKuota) VALUES
 (8, N'RUMAH_TANGGA', @p, 4),   -- LPG 3 kg: 4 tabung/bulan
 (8, N'USAHA_MIKRO',  @p, 10),
 (2, N'RUMAH_TANGGA', @p, 2),   -- Beras SPHP: 2 bungkus/bulan
 (6, N'PETANI',       @p, 4),   -- Urea: 4 sak/bulan
 (7, N'PETANI',       @p, 3);   -- NPK: 3 sak/bulan
GO
