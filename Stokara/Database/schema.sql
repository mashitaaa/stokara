/* =====================================================================
   STOKARA - Skema Database (SQL Server 2019+ / Express)
   Sesuai PRD STOKARA v1.1 bagian 12.
   Script ini dijalankan pada database STOKARA_DB yang sudah ada.
   Aplikasi menjalankan script ini otomatis saat pertama kali dibuka
   (lihat Infrastructure/DbInitializer.cs). Untuk menjalankan manual:
     CREATE DATABASE STOKARA_DB;  lalu  USE STOKARA_DB;  lalu script ini.
   ===================================================================== */

/* ---------- Pengguna dan Pengaturan ---------- */
CREATE TABLE dbo.Users (
    UserId            INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Username          NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash      NVARCHAR(256) NOT NULL,
    Nama              NVARCHAR(100) NOT NULL,
    Role              NVARCHAR(20)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('MANAJER','KASIR','GUDANG','PENGURUS')),
    Tema              NVARCHAR(10)  NOT NULL CONSTRAINT DF_Users_Tema DEFAULT ('Light'),
    IsActive          BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    FailedLoginCount  INT           NOT NULL CONSTRAINT DF_Users_Failed DEFAULT (0),
    LockedUntil       DATETIME2     NULL,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSDATETIME()),
    LastLoginAt       DATETIME2     NULL
);
GO

CREATE TABLE dbo.Pengaturan (
    Kunci  NVARCHAR(50)  NOT NULL CONSTRAINT PK_Pengaturan PRIMARY KEY,
    Nilai  NVARCHAR(400) NULL
);
GO

CREATE TABLE dbo.Aktivitas (
    AktivitasId  BIGINT IDENTITY(1,1) CONSTRAINT PK_Aktivitas PRIMARY KEY,
    UserId       INT           NULL CONSTRAINT FK_Aktivitas_Users REFERENCES dbo.Users(UserId),
    JenisAksi    NVARCHAR(40)  NOT NULL,
    Entitas      NVARCHAR(40)  NOT NULL,
    EntitasId    NVARCHAR(40)  NULL,
    Keterangan   NVARCHAR(400) NULL,
    Waktu        DATETIME2     NOT NULL CONSTRAINT DF_Aktivitas_Waktu DEFAULT (SYSDATETIME())
);
CREATE INDEX IX_Aktivitas_Waktu ON dbo.Aktivitas(Waktu DESC);
GO

/* Penomoran dokumen per prefix per hari (PJ-20261001-0001) */
CREATE TABLE dbo.NomorUrut (
    Prefix    NVARCHAR(4) NOT NULL,
    Tanggal   DATE        NOT NULL,
    Terakhir  INT         NOT NULL,
    CONSTRAINT PK_NomorUrut PRIMARY KEY (Prefix, Tanggal)
);
GO

/* ---------- Master data ---------- */
CREATE TABLE dbo.Kategori (
    KategoriId  INT IDENTITY(1,1) CONSTRAINT PK_Kategori PRIMARY KEY,
    Nama        NVARCHAR(60) NOT NULL CONSTRAINT UQ_Kategori_Nama UNIQUE,
    IsActive    BIT NOT NULL CONSTRAINT DF_Kategori_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Satuan (
    SatuanId  INT IDENTITY(1,1) CONSTRAINT PK_Satuan PRIMARY KEY,
    Nama      NVARCHAR(30) NOT NULL CONSTRAINT UQ_Satuan_Nama UNIQUE,
    IsActive  BIT NOT NULL CONSTRAINT DF_Satuan_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Pemasok (
    PemasokId  INT IDENTITY(1,1) CONSTRAINT PK_Pemasok PRIMARY KEY,
    Kode       NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Pemasok_Kode UNIQUE,
    Nama       NVARCHAR(100) NOT NULL,
    Jenis      NVARCHAR(10)  NOT NULL CONSTRAINT CK_Pemasok_Jenis CHECK (Jenis IN ('BUMN','BUMD','LOKAL')),
    Telepon    NVARCHAR(30)  NULL,
    Alamat     NVARCHAR(200) NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Pemasok_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Penitip (
    PenitipId  INT IDENTITY(1,1) CONSTRAINT PK_Penitip PRIMARY KEY,
    Nama       NVARCHAR(100) NOT NULL,
    Telepon    NVARCHAR(30)  NULL,
    Alamat     NVARCHAR(200) NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Penitip_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Anggota (
    AnggotaId         INT IDENTITY(1,1) CONSTRAINT PK_Anggota PRIMARY KEY,
    NomorAnggota      NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Anggota_Nomor UNIQUE,
    NIK               CHAR(16)      NOT NULL CONSTRAINT UQ_Anggota_NIK UNIQUE,
    Nama              NVARCHAR(100) NOT NULL,
    Alamat            NVARCHAR(200) NULL,
    Dusun             NVARCHAR(60)  NULL,
    Telepon           NVARCHAR(30)  NULL,
    Status            NVARCHAR(10)  NOT NULL CONSTRAINT DF_Anggota_Status DEFAULT ('AKTIF')
                      CONSTRAINT CK_Anggota_Status CHECK (Status IN ('AKTIF','NONAKTIF')),
    TanggalBergabung  DATE NOT NULL CONSTRAINT DF_Anggota_Tgl DEFAULT (CAST(GETDATE() AS DATE)),
    CONSTRAINT CK_Anggota_NIK CHECK (NIK NOT LIKE '%[^0-9]%')
);

CREATE TABLE dbo.PenerimaSubsidi (
    PenerimaId     INT IDENTITY(1,1) CONSTRAINT PK_PenerimaSubsidi PRIMARY KEY,
    NIK            CHAR(16)      NOT NULL CONSTRAINT UQ_Penerima_NIK UNIQUE,
    Nama           NVARCHAR(100) NOT NULL,
    JenisPenerima  NVARCHAR(20)  NOT NULL CONSTRAINT CK_Penerima_Jenis
                   CHECK (JenisPenerima IN ('PETANI','RUMAH_TANGGA','USAHA_MIKRO','LAINNYA')),
    Dusun          NVARCHAR(60)  NULL,
    AnggotaId      INT NULL CONSTRAINT FK_Penerima_Anggota REFERENCES dbo.Anggota(AnggotaId),
    Status         NVARCHAR(10)  NOT NULL CONSTRAINT DF_Penerima_Status DEFAULT ('AKTIF')
                   CONSTRAINT CK_Penerima_Status CHECK (Status IN ('AKTIF','NONAKTIF')),
    CONSTRAINT CK_Penerima_NIK CHECK (NIK NOT LIKE '%[^0-9]%')
);
GO

CREATE TABLE dbo.Barang (
    BarangId        INT IDENTITY(1,1) CONSTRAINT PK_Barang PRIMARY KEY,
    SKU             NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Barang_SKU UNIQUE,
    Barcode         NVARCHAR(50)  NULL,
    Nama            NVARCHAR(120) NOT NULL,
    KategoriId      INT NOT NULL CONSTRAINT FK_Barang_Kategori REFERENCES dbo.Kategori(KategoriId),
    SatuanId        INT NOT NULL CONSTRAINT FK_Barang_Satuan REFERENCES dbo.Satuan(SatuanId),
    Kepemilikan     NVARCHAR(10)  NOT NULL CONSTRAINT CK_Barang_Kepemilikan CHECK (Kepemilikan IN ('MILIK','TITIPAN')),
    PenitipId       INT NULL CONSTRAINT FK_Barang_Penitip REFERENCES dbo.Penitip(PenitipId),
    HargaTitip      DECIMAL(18,2) NULL,
    HargaAnggota    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Barang_HA DEFAULT (0),
    HargaUmum       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Barang_HU DEFAULT (0),
    HargaPokokRata  DECIMAL(18,4) NOT NULL CONSTRAINT DF_Barang_HPP DEFAULT (0),
    IsSubsidi       BIT NOT NULL CONSTRAINT DF_Barang_Subsidi DEFAULT (0),
    HET             DECIMAL(18,2) NULL,
    StokMinimum     DECIMAL(18,2) NOT NULL CONSTRAINT DF_Barang_Min DEFAULT (0),
    StokSaatIni     DECIMAL(18,2) NOT NULL CONSTRAINT DF_Barang_Stok DEFAULT (0),
    IsActive        BIT NOT NULL CONSTRAINT DF_Barang_IsActive DEFAULT (1),
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_Barang_Created DEFAULT (SYSDATETIME()),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT CK_Barang_Stok      CHECK (StokSaatIni >= 0),
    CONSTRAINT CK_Barang_Harga     CHECK (HargaAnggota >= 0 AND HargaUmum >= 0 AND StokMinimum >= 0),
    /* BR-07 / A-02: barang subsidi wajib HET > 0 dan satu harga jual */
    CONSTRAINT CK_Barang_Subsidi   CHECK (IsSubsidi = 0 OR (HET IS NOT NULL AND HET > 0 AND HargaAnggota = HargaUmum)),
    /* A-05 / A-06: barang titipan wajib punya penitip dan harga titip <= harga jual */
    CONSTRAINT CK_Barang_Titipan   CHECK (Kepemilikan = 'MILIK'
                                         OR (PenitipId IS NOT NULL AND HargaTitip IS NOT NULL AND HargaTitip >= 0
                                             AND HargaTitip <= HargaUmum AND HargaTitip <= HargaAnggota)),
    CONSTRAINT CK_Barang_SubsidiMilik CHECK (NOT (IsSubsidi = 1 AND Kepemilikan = 'TITIPAN'))
);
CREATE UNIQUE INDEX UX_Barang_Barcode ON dbo.Barang(Barcode) WHERE Barcode IS NOT NULL;
CREATE INDEX IX_Barang_Nama ON dbo.Barang(Nama);
GO

/* ---------- Kuota subsidi (A-03) ---------- */
CREATE TABLE dbo.AturanKuota (
    AturanId       INT IDENTITY(1,1) CONSTRAINT PK_AturanKuota PRIMARY KEY,
    BarangId       INT NOT NULL CONSTRAINT FK_Aturan_Barang REFERENCES dbo.Barang(BarangId),
    JenisPenerima  NVARCHAR(20) NOT NULL CONSTRAINT CK_Aturan_Jenis
                   CHECK (JenisPenerima IN ('PETANI','RUMAH_TANGGA','USAHA_MIKRO','LAINNYA')),
    Periode        NVARCHAR(7)  NOT NULL,   -- 'yyyy-MM' (bulanan) atau 'yyyy' (tahunan)
    JumlahKuota    DECIMAL(18,2) NOT NULL CONSTRAINT CK_Aturan_Jumlah CHECK (JumlahKuota > 0),
    CONSTRAINT UQ_AturanKuota UNIQUE (BarangId, JenisPenerima, Periode)
);

CREATE TABLE dbo.PemakaianKuota (
    PemakaianId     INT IDENTITY(1,1) CONSTRAINT PK_PemakaianKuota PRIMARY KEY,
    PenerimaId      INT NOT NULL CONSTRAINT FK_Pemakaian_Penerima REFERENCES dbo.PenerimaSubsidi(PenerimaId),
    BarangId        INT NOT NULL CONSTRAINT FK_Pemakaian_Barang REFERENCES dbo.Barang(BarangId),
    Periode         NVARCHAR(7) NOT NULL,
    JumlahTerpakai  DECIMAL(18,2) NOT NULL CONSTRAINT DF_Pemakaian_Jumlah DEFAULT (0)
                    CONSTRAINT CK_Pemakaian_Jumlah CHECK (JumlahTerpakai >= 0),
    CONSTRAINT UQ_PemakaianKuota UNIQUE (PenerimaId, BarangId, Periode)
);
CREATE INDEX IX_Pemakaian_Penerima ON dbo.PemakaianKuota(PenerimaId, Periode);
GO

/* ---------- Penerimaan barang ---------- */
CREATE TABLE dbo.Penerimaan (
    PenerimaanId     INT IDENTITY(1,1) CONSTRAINT PK_Penerimaan PRIMARY KEY,
    NomorDokumen     NVARCHAR(30) NOT NULL CONSTRAINT UQ_Penerimaan_Nomor UNIQUE,
    Tanggal          DATETIME2    NOT NULL,
    Sumber           NVARCHAR(15) NOT NULL CONSTRAINT CK_Penerimaan_Sumber
                     CHECK (Sumber IN ('STOK_AWAL','PEMBELIAN','SUBSIDI','KONSINYASI')),
    PemasokId        INT NULL CONSTRAINT FK_Penerimaan_Pemasok REFERENCES dbo.Pemasok(PemasokId),
    PenitipId        INT NULL CONSTRAINT FK_Penerimaan_Penitip REFERENCES dbo.Penitip(PenitipId),
    NomorSuratJalan  NVARCHAR(50)  NULL,
    Catatan          NVARCHAR(200) NULL,
    Total            DECIMAL(18,2) NOT NULL CONSTRAINT DF_Penerimaan_Total DEFAULT (0),
    Status           NVARCHAR(12)  NOT NULL CONSTRAINT DF_Penerimaan_Status DEFAULT ('AKTIF')
                     CONSTRAINT CK_Penerimaan_Status CHECK (Status IN ('AKTIF','DIBATALKAN')),
    UserId           INT NOT NULL CONSTRAINT FK_Penerimaan_User REFERENCES dbo.Users(UserId),
    AlasanBatal      NVARCHAR(200) NULL,
    DibatalkanOleh   INT NULL CONSTRAINT FK_Penerimaan_UserBatal REFERENCES dbo.Users(UserId),
    DibatalkanPada   DATETIME2 NULL,
    CONSTRAINT CK_Penerimaan_Mitra CHECK (
        (Sumber = 'KONSINYASI' AND PenitipId IS NOT NULL AND PemasokId IS NULL) OR
        (Sumber IN ('PEMBELIAN','SUBSIDI') AND PemasokId IS NOT NULL AND PenitipId IS NULL) OR
        (Sumber = 'STOK_AWAL' AND PenitipId IS NULL))
);

CREATE TABLE dbo.PenerimaanDetail (
    DetailId            INT IDENTITY(1,1) CONSTRAINT PK_PenerimaanDetail PRIMARY KEY,
    PenerimaanId        INT NOT NULL CONSTRAINT FK_PenDetail_Penerimaan REFERENCES dbo.Penerimaan(PenerimaanId),
    BarangId            INT NOT NULL CONSTRAINT FK_PenDetail_Barang REFERENCES dbo.Barang(BarangId),
    Jumlah              DECIMAL(18,2) NOT NULL CONSTRAINT CK_PenDetail_Jumlah CHECK (Jumlah > 0),
    HargaBeli           DECIMAL(18,2) NOT NULL CONSTRAINT CK_PenDetail_Harga CHECK (HargaBeli >= 0),
    TanggalKedaluwarsa  DATE NULL
);
CREATE INDEX IX_PenDetail_Barang ON dbo.PenerimaanDetail(BarangId);
CREATE INDEX IX_Penerimaan_Tanggal ON dbo.Penerimaan(Tanggal);
GO

/* ---------- Penjualan dan penyaluran (A-10) ---------- */
CREATE TABLE dbo.Penjualan (
    PenjualanId     INT IDENTITY(1,1) CONSTRAINT PK_Penjualan PRIMARY KEY,
    NomorDokumen    NVARCHAR(30) NOT NULL CONSTRAINT UQ_Penjualan_Nomor UNIQUE,
    Tanggal         DATETIME2    NOT NULL,
    Jenis           NVARCHAR(20) NOT NULL CONSTRAINT CK_Penjualan_Jenis CHECK (Jenis IN ('PENJUALAN','PENYALURAN_SUBSIDI')),
    JenisPelanggan  NVARCHAR(10) NOT NULL CONSTRAINT CK_Penjualan_Pelanggan CHECK (JenisPelanggan IN ('UMUM','ANGGOTA')),
    AnggotaId       INT NULL CONSTRAINT FK_Penjualan_Anggota REFERENCES dbo.Anggota(AnggotaId),
    PenerimaId      INT NULL CONSTRAINT FK_Penjualan_Penerima REFERENCES dbo.PenerimaSubsidi(PenerimaId),
    Total           DECIMAL(18,2) NOT NULL,
    UangDiterima    DECIMAL(18,2) NOT NULL,
    Kembalian       DECIMAL(18,2) NOT NULL,
    Status          NVARCHAR(12)  NOT NULL CONSTRAINT DF_Penjualan_Status DEFAULT ('AKTIF')
                    CONSTRAINT CK_Penjualan_Status CHECK (Status IN ('AKTIF','DIBATALKAN')),
    UserId          INT NOT NULL CONSTRAINT FK_Penjualan_User REFERENCES dbo.Users(UserId),
    AlasanBatal     NVARCHAR(200) NULL,
    DibatalkanOleh  INT NULL CONSTRAINT FK_Penjualan_UserBatal REFERENCES dbo.Users(UserId),
    DibatalkanPada  DATETIME2 NULL,
    CONSTRAINT CK_Penjualan_Pihak CHECK (
        (Jenis = 'PENJUALAN' AND PenerimaId IS NULL AND
            ((JenisPelanggan = 'ANGGOTA' AND AnggotaId IS NOT NULL) OR (JenisPelanggan = 'UMUM' AND AnggotaId IS NULL)))
        OR (Jenis = 'PENYALURAN_SUBSIDI' AND PenerimaId IS NOT NULL AND AnggotaId IS NULL AND JenisPelanggan = 'UMUM')),
    CONSTRAINT CK_Penjualan_Uang CHECK (Total >= 0 AND UangDiterima >= Total AND Kembalian >= 0)
);
CREATE INDEX IX_Penjualan_Tanggal ON dbo.Penjualan(Tanggal);

CREATE TABLE dbo.PenjualanDetail (
    DetailId     INT IDENTITY(1,1) CONSTRAINT PK_PenjualanDetail PRIMARY KEY,
    PenjualanId  INT NOT NULL CONSTRAINT FK_PjDetail_Penjualan REFERENCES dbo.Penjualan(PenjualanId),
    BarangId     INT NOT NULL CONSTRAINT FK_PjDetail_Barang REFERENCES dbo.Barang(BarangId),
    Jumlah       DECIMAL(18,2) NOT NULL CONSTRAINT CK_PjDetail_Jumlah CHECK (Jumlah > 0),
    HargaSatuan  DECIMAL(18,2) NOT NULL CONSTRAINT CK_PjDetail_Harga CHECK (HargaSatuan >= 0),
    HargaPokok   DECIMAL(18,4) NOT NULL,   -- salinan HPP (A-08); untuk titipan = harga titip
    Subtotal     DECIMAL(18,2) NOT NULL,
    PeriodeKuota NVARCHAR(7) NULL           -- terisi untuk penyaluran subsidi
);
CREATE INDEX IX_PjDetail_Penjualan ON dbo.PenjualanDetail(PenjualanId);
CREATE INDEX IX_PjDetail_Barang ON dbo.PenjualanDetail(BarangId);
GO

/* ---------- Konsinyasi (A-05, A-07) ---------- */
CREATE TABLE dbo.HutangPenitip (
    HutangId           INT IDENTITY(1,1) CONSTRAINT PK_HutangPenitip PRIMARY KEY,
    PenitipId          INT NOT NULL CONSTRAINT FK_Hutang_Penitip REFERENCES dbo.Penitip(PenitipId),
    PenjualanDetailId  INT NOT NULL CONSTRAINT FK_Hutang_PjDetail REFERENCES dbo.PenjualanDetail(DetailId)
                       CONSTRAINT UQ_Hutang_PjDetail UNIQUE,
    BarangId           INT NOT NULL CONSTRAINT FK_Hutang_Barang REFERENCES dbo.Barang(BarangId),
    Jumlah             DECIMAL(18,2) NOT NULL,
    HargaTitip         DECIMAL(18,2) NOT NULL,
    NilaiHutang        DECIMAL(18,2) NOT NULL,
    Komisi             DECIMAL(18,2) NOT NULL,
    JumlahDibayar      DECIMAL(18,2) NOT NULL CONSTRAINT DF_Hutang_Dibayar DEFAULT (0),
    Status             NVARCHAR(10)  NOT NULL CONSTRAINT DF_Hutang_Status DEFAULT ('BELUM')
                       CONSTRAINT CK_Hutang_Status CHECK (Status IN ('BELUM','SEBAGIAN','LUNAS','BATAL')),
    CreatedAt          DATETIME2 NOT NULL CONSTRAINT DF_Hutang_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Hutang_Bayar CHECK (JumlahDibayar >= 0 AND JumlahDibayar <= NilaiHutang)
);
CREATE INDEX IX_Hutang_Penitip ON dbo.HutangPenitip(PenitipId, Status);

CREATE TABLE dbo.PelunasanPenitip (
    PelunasanId   INT IDENTITY(1,1) CONSTRAINT PK_PelunasanPenitip PRIMARY KEY,
    NomorDokumen  NVARCHAR(30) NOT NULL CONSTRAINT UQ_Pelunasan_Nomor UNIQUE,
    PenitipId     INT NOT NULL CONSTRAINT FK_Pelunasan_Penitip REFERENCES dbo.Penitip(PenitipId),
    Tanggal       DATETIME2 NOT NULL,
    JumlahBayar   DECIMAL(18,2) NOT NULL CONSTRAINT CK_Pelunasan_Jumlah CHECK (JumlahBayar > 0),
    Catatan       NVARCHAR(200) NULL,
    UserId        INT NOT NULL CONSTRAINT FK_Pelunasan_User REFERENCES dbo.Users(UserId)
);

CREATE TABLE dbo.PelunasanAlokasi (
    PelunasanId  INT NOT NULL CONSTRAINT FK_Alokasi_Pelunasan REFERENCES dbo.PelunasanPenitip(PelunasanId),
    HutangId     INT NOT NULL CONSTRAINT FK_Alokasi_Hutang REFERENCES dbo.HutangPenitip(HutangId),
    Jumlah       DECIMAL(18,2) NOT NULL CONSTRAINT CK_Alokasi_Jumlah CHECK (Jumlah > 0),
    CONSTRAINT PK_PelunasanAlokasi PRIMARY KEY (PelunasanId, HutangId)
);
GO

/* ---------- Pengeluaran non-penjualan ---------- */
CREATE TABLE dbo.PengeluaranLain (
    PengeluaranId  INT IDENTITY(1,1) CONSTRAINT PK_PengeluaranLain PRIMARY KEY,
    NomorDokumen   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Pengeluaran_Nomor UNIQUE,
    Tanggal        DATETIME2 NOT NULL,
    Jenis          NVARCHAR(15) NOT NULL CONSTRAINT CK_Pengeluaran_Jenis
                   CHECK (Jenis IN ('RETUR_PEMASOK','RETUR_PENITIP','RUSAK','KEDALUWARSA','HILANG')),
    PemasokId      INT NULL CONSTRAINT FK_Pengeluaran_Pemasok REFERENCES dbo.Pemasok(PemasokId),
    PenitipId      INT NULL CONSTRAINT FK_Pengeluaran_Penitip REFERENCES dbo.Penitip(PenitipId),
    Alasan         NVARCHAR(200) NOT NULL CONSTRAINT CK_Pengeluaran_Alasan CHECK (LEN(Alasan) > 0),
    UserId         INT NOT NULL CONSTRAINT FK_Pengeluaran_User REFERENCES dbo.Users(UserId)
);

CREATE TABLE dbo.PengeluaranDetail (
    DetailId       INT IDENTITY(1,1) CONSTRAINT PK_PengeluaranDetail PRIMARY KEY,
    PengeluaranId  INT NOT NULL CONSTRAINT FK_PgDetail_Pengeluaran REFERENCES dbo.PengeluaranLain(PengeluaranId),
    BarangId       INT NOT NULL CONSTRAINT FK_PgDetail_Barang REFERENCES dbo.Barang(BarangId),
    Jumlah         DECIMAL(18,2) NOT NULL CONSTRAINT CK_PgDetail_Jumlah CHECK (Jumlah > 0)
);
GO

/* ---------- Mutasi stok (kartu stok) ---------- */
CREATE TABLE dbo.MutasiStok (
    MutasiId      BIGINT IDENTITY(1,1) CONSTRAINT PK_MutasiStok PRIMARY KEY,
    BarangId      INT NOT NULL CONSTRAINT FK_Mutasi_Barang REFERENCES dbo.Barang(BarangId),
    Tanggal       DATETIME2 NOT NULL,
    JenisMutasi   NVARCHAR(25) NOT NULL CONSTRAINT CK_Mutasi_Jenis CHECK (JenisMutasi IN (
                  'MASUK_STOK_AWAL','MASUK_PEMBELIAN','MASUK_SUBSIDI','MASUK_KONSINYASI',
                  'KELUAR_JUAL','KELUAR_SUBSIDI','KELUAR_RETUR','KELUAR_RUSAK','VOID','PENYESUAIAN')),
    RefTipe       NVARCHAR(20) NOT NULL,
    RefId         INT NOT NULL,
    NomorDokumen  NVARCHAR(30) NOT NULL,
    Masuk         DECIMAL(18,2) NOT NULL CONSTRAINT DF_Mutasi_Masuk DEFAULT (0),
    Keluar        DECIMAL(18,2) NOT NULL CONSTRAINT DF_Mutasi_Keluar DEFAULT (0),
    SaldoSetelah  DECIMAL(18,2) NOT NULL,
    Keterangan    NVARCHAR(200) NULL,
    UserId        INT NOT NULL CONSTRAINT FK_Mutasi_User REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Mutasi_Nilai CHECK (Masuk >= 0 AND Keluar >= 0 AND SaldoSetelah >= 0)
);
CREATE INDEX IX_Mutasi_Barang_Tanggal ON dbo.MutasiStok(BarangId, Tanggal, MutasiId);
GO

/* ---------- Stok opname ---------- */
CREATE TABLE dbo.StokOpname (
    OpnameId       INT IDENTITY(1,1) CONSTRAINT PK_StokOpname PRIMARY KEY,
    NomorDokumen   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Opname_Nomor UNIQUE,
    Tanggal        DATETIME2 NOT NULL,
    KategoriId     INT NULL CONSTRAINT FK_Opname_Kategori REFERENCES dbo.Kategori(KategoriId),
    Status         NVARCHAR(10) NOT NULL CONSTRAINT DF_Opname_Status DEFAULT ('DRAFT')
                   CONSTRAINT CK_Opname_Status CHECK (Status IN ('DRAFT','DIAJUKAN','DISETUJUI','DITOLAK')),
    Catatan        NVARCHAR(200) NULL,
    DibuatOleh     INT NOT NULL CONSTRAINT FK_Opname_Pembuat REFERENCES dbo.Users(UserId),
    DiajukanOleh   INT NULL CONSTRAINT FK_Opname_Pengaju REFERENCES dbo.Users(UserId),
    DiajukanPada   DATETIME2 NULL,
    DisetujuiOleh  INT NULL CONSTRAINT FK_Opname_Penyetuju REFERENCES dbo.Users(UserId),
    DisetujuiPada  DATETIME2 NULL,
    AlasanTolak    NVARCHAR(200) NULL,
    /* BR-15: penyetuju bukan pengaju */
    CONSTRAINT CK_Opname_Penyetuju CHECK (DisetujuiOleh IS NULL OR DiajukanOleh IS NULL OR DisetujuiOleh <> DiajukanOleh)
);

CREATE TABLE dbo.StokOpnameDetail (
    DetailId    INT IDENTITY(1,1) CONSTRAINT PK_StokOpnameDetail PRIMARY KEY,
    OpnameId    INT NOT NULL CONSTRAINT FK_OpDetail_Opname REFERENCES dbo.StokOpname(OpnameId),
    BarangId    INT NOT NULL CONSTRAINT FK_OpDetail_Barang REFERENCES dbo.Barang(BarangId),
    StokSistem  DECIMAL(18,2) NOT NULL,
    StokFisik   DECIMAL(18,2) NULL CONSTRAINT CK_OpDetail_Fisik CHECK (StokFisik IS NULL OR StokFisik >= 0),
    Selisih     AS (StokFisik - StokSistem) PERSISTED,
    Alasan      NVARCHAR(200) NULL,
    CONSTRAINT UQ_OpDetail UNIQUE (OpnameId, BarangId)
);
GO

/* ---------- Tutup kasir harian (A-17) ---------- */
CREATE TABLE dbo.TutupHari (
    Tanggal          DATE NOT NULL CONSTRAINT PK_TutupHari PRIMARY KEY,
    JumlahTransaksi  INT NOT NULL,
    TotalPenjualan   DECIMAL(18,2) NOT NULL,
    ModalAwal        DECIMAL(18,2) NOT NULL CONSTRAINT CK_Tutup_Modal CHECK (ModalAwal >= 0),
    UangSeharusnya   DECIMAL(18,2) NOT NULL,
    UangFisik        DECIMAL(18,2) NOT NULL CONSTRAINT CK_Tutup_Fisik CHECK (UangFisik >= 0),
    Selisih          AS (UangFisik - UangSeharusnya) PERSISTED,
    Catatan          NVARCHAR(200) NULL,
    UserId           INT NOT NULL CONSTRAINT FK_Tutup_User REFERENCES dbo.Users(UserId),
    DitutupPada      DATETIME2 NOT NULL CONSTRAINT DF_Tutup_Pada DEFAULT (SYSDATETIME())
);
GO
