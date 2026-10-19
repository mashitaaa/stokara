using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

public sealed class BarangFilter
{
    public string Keyword { get; set; }
    public int? KategoriId { get; set; }
    /// <summary>SEMUA, BEBAS, SUBSIDI, TITIPAN</summary>
    public string Jenis { get; set; } = "SEMUA";
    /// <summary>SEMUA, Aman, Menipis, Habis</summary>
    public string StatusStok { get; set; } = "SEMUA";
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string SortBy { get; set; } = "Nama";
    public bool SortDesc { get; set; }
}

/// <summary>Akses data barang, perubahan stok atomik, dan kartu stok.</summary>
public sealed class BarangRepository
{
    private readonly Db _db;
    public BarangRepository(Db db) => _db = db;

    private const string Select = @"
SELECT b.BarangId, b.SKU, b.Barcode, b.Nama, b.KategoriId, k.Nama AS KategoriNama, b.SatuanId, s.Nama AS SatuanNama,
       b.Kepemilikan, b.PenitipId, p.Nama AS PenitipNama, b.HargaTitip, b.HargaAnggota, b.HargaUmum, b.HargaPokokRata,
       b.IsSubsidi, b.HET, b.StokMinimum, b.StokSaatIni, b.IsActive
FROM dbo.Barang b
JOIN dbo.Kategori k ON k.KategoriId = b.KategoriId
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
LEFT JOIN dbo.Penitip p ON p.PenitipId = b.PenitipId";

    public static Barang Map(SqlDataReader r) => new()
    {
        BarangId = r.Int("BarangId"),
        SKU = r.Str("SKU"),
        Barcode = r.Str("Barcode"),
        Nama = r.Str("Nama"),
        KategoriId = r.Int("KategoriId"),
        KategoriNama = r.Str("KategoriNama"),
        SatuanId = r.Int("SatuanId"),
        SatuanNama = r.Str("SatuanNama"),
        Kepemilikan = r.Str("Kepemilikan"),
        PenitipId = r.IntN("PenitipId"),
        PenitipNama = r.Str("PenitipNama"),
        HargaTitip = r.DecN("HargaTitip"),
        HargaAnggota = r.Dec("HargaAnggota"),
        HargaUmum = r.Dec("HargaUmum"),
        HargaPokokRata = r.Dec("HargaPokokRata"),
        IsSubsidi = r.Bool("IsSubsidi"),
        HET = r.DecN("HET"),
        StokMinimum = r.Dec("StokMinimum"),
        StokSaatIni = r.Dec("StokSaatIni"),
        IsActive = r.Bool("IsActive")
    };

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU"] = "b.SKU", ["Nama"] = "b.Nama", ["KategoriNama"] = "k.Nama", ["StokSaatIni"] = "b.StokSaatIni",
        ["HargaUmum"] = "b.HargaUmum", ["HargaAnggota"] = "b.HargaAnggota", ["StatusStok"] = "b.StokSaatIni - b.StokMinimum",
        ["JenisLabel"] = "b.IsSubsidi DESC, b.Kepemilikan"
    };

    /// <summary>Daftar barang dengan filter, urut, dan pagination di server (FR-08).</summary>
    public async Task<(List<Barang> Items, int Total)> SearchAsync(BarangFilter f)
    {
        var where = new List<string>();
        if (!f.IncludeInactive) where.Add("b.IsActive = 1");
        if (!string.IsNullOrWhiteSpace(f.Keyword))
            where.Add("(b.Nama LIKE @Kw OR b.SKU LIKE @Kw OR b.Barcode LIKE @Kw)");
        if (f.KategoriId.HasValue) where.Add("b.KategoriId = @KategoriId");
        switch (f.Jenis)
        {
            case "BEBAS": where.Add("b.IsSubsidi = 0 AND b.Kepemilikan = 'MILIK'"); break;
            case "SUBSIDI": where.Add("b.IsSubsidi = 1"); break;
            case "TITIPAN": where.Add("b.Kepemilikan = 'TITIPAN'"); break;
        }
        switch (f.StatusStok)
        {
            case "Habis": where.Add("b.StokSaatIni = 0"); break;
            case "Menipis": where.Add("b.StokSaatIni > 0 AND b.StokSaatIni <= b.StokMinimum"); break;
            case "Aman": where.Add("b.StokSaatIni > b.StokMinimum"); break;
        }
        var whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";
        var sortCol = SortColumns.TryGetValue(f.SortBy ?? "Nama", out var c) ? c : "b.Nama";
        var dir = f.SortDesc && !sortCol.Contains(' ') ? " DESC" : "";
        var args = new
        {
            Kw = "%" + (f.Keyword ?? "").Trim() + "%",
            KategoriId = f.KategoriId,
            Skip = Math.Max(0, (f.Page - 1) * f.PageSize),
            Take = f.PageSize
        };

        await using var conn = await _db.OpenAsync();
        var total = await conn.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Barang b JOIN dbo.Kategori k ON k.KategoriId = b.KategoriId" + whereSql, args);
        var items = await conn.QueryAsync(
            Select + whereSql + $" ORDER BY {sortCol}{dir}, b.BarangId OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
            Map, args);
        return (items, total);
    }

    /// <summary>Pencarian cepat untuk kasir dan form (nama/SKU/barcode). jenis: null, SUBSIDI, NONSUBSIDI, MILIK, TITIPAN.</summary>
    public async Task<List<Barang>> QuickSearchAsync(string keyword, string jenis = null, int? penitipId = null, int top = 12)
    {
        var where = "b.IsActive = 1 AND (b.Nama LIKE @Kw OR b.SKU LIKE @Kw OR b.Barcode LIKE @Kw)";
        where += jenis switch
        {
            "SUBSIDI" => " AND b.IsSubsidi = 1",
            "NONSUBSIDI" => " AND b.IsSubsidi = 0",
            "MILIK" => " AND b.Kepemilikan = 'MILIK'",
            "MILIK_NONSUBSIDI" => " AND b.Kepemilikan = 'MILIK' AND b.IsSubsidi = 0",
            "TITIPAN" => " AND b.Kepemilikan = 'TITIPAN'",
            _ => ""
        };
        if (penitipId.HasValue) where += " AND b.PenitipId = @PenitipId";
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(
            Select.Replace("SELECT b.BarangId", $"SELECT TOP ({top}) b.BarangId") + " WHERE " + where + " ORDER BY b.Nama",
            Map, new { Kw = "%" + (keyword ?? "").Trim() + "%", PenitipId = penitipId });
    }

    /// <summary>Cocok persis barcode atau SKU (untuk barcode scanner, FR-17).</summary>
    public async Task<Barang> GetByCodeAsync(string code)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(Select + " WHERE b.IsActive = 1 AND (b.Barcode = @Code OR b.SKU = @Code)",
            Map, new { Code = (code ?? "").Trim() });
    }

    public async Task<Barang> GetAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(Select + " WHERE b.BarangId = @Id", Map, new { Id = id });
    }

    /// <summary>Membaca barang di dalam transaksi dengan kunci baris agar konsisten.</summary>
    public Task<Barang> GetForUpdateAsync(SqlConnection conn, SqlTransaction tx, int id)
        => conn.QuerySingleAsync(Select.Replace("FROM dbo.Barang b", "FROM dbo.Barang b WITH (UPDLOCK, ROWLOCK)")
                                 + " WHERE b.BarangId = @Id", Map, new { Id = id }, tx);

    public async Task<List<Barang>> ListAsync(string jenis = null, int? penitipId = null, bool activeOnly = true)
    {
        var where = activeOnly ? " WHERE b.IsActive = 1" : " WHERE 1 = 1";
        if (jenis == "SUBSIDI") where += " AND b.IsSubsidi = 1";
        if (jenis == "TITIPAN") where += " AND b.Kepemilikan = 'TITIPAN'";
        if (jenis == "MILIK") where += " AND b.Kepemilikan = 'MILIK'";
        if (penitipId.HasValue) where += " AND b.PenitipId = @PenitipId";
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(Select + where + " ORDER BY b.Nama", Map, new { PenitipId = penitipId });
    }

    public async Task<bool> ExistsAsync(string column, string value, int excludeId)
    {
        if (column != "SKU" && column != "Barcode") throw new ArgumentException("Kolom tidak dikenal", nameof(column));
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Barang WHERE {column} = @Value AND BarangId <> @Id",
            new { Value = value, Id = excludeId }) > 0;
    }

    public async Task<int> InsertAsync(Barang b)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.Barang (SKU, Barcode, Nama, KategoriId, SatuanId, Kepemilikan, PenitipId, HargaTitip,
                        HargaAnggota, HargaUmum, IsSubsidi, HET, StokMinimum, IsActive)
OUTPUT inserted.BarangId
VALUES (@SKU, @Barcode, @Nama, @KategoriId, @SatuanId, @Kepemilikan, @PenitipId, @HargaTitip,
        @HargaAnggota, @HargaUmum, @IsSubsidi, @HET, @StokMinimum, 1)", Args(b));
    }

    /// <summary>Mengubah data barang. StokSaatIni dan HargaPokokRata tidak pernah diubah di sini (BR-01).</summary>
    public async Task UpdateAsync(Barang b)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync(@"
UPDATE dbo.Barang SET SKU = @SKU, Barcode = @Barcode, Nama = @Nama, KategoriId = @KategoriId, SatuanId = @SatuanId,
       Kepemilikan = @Kepemilikan, PenitipId = @PenitipId, HargaTitip = @HargaTitip, HargaAnggota = @HargaAnggota,
       HargaUmum = @HargaUmum, IsSubsidi = @IsSubsidi, HET = @HET, StokMinimum = @StokMinimum, UpdatedAt = SYSDATETIME()
WHERE BarangId = @BarangId", Args(b));
    }

    private static object Args(Barang b) => new
    {
        b.BarangId,
        b.SKU,
        Barcode = string.IsNullOrWhiteSpace(b.Barcode) ? null : b.Barcode.Trim(),
        b.Nama,
        b.KategoriId,
        b.SatuanId,
        b.Kepemilikan,
        PenitipId = b.IsTitipan ? b.PenitipId : null,
        HargaTitip = b.IsTitipan ? b.HargaTitip : null,
        b.HargaAnggota,
        b.HargaUmum,
        b.IsSubsidi,
        HET = b.IsSubsidi ? b.HET : null,
        b.StokMinimum
    };

    public async Task SetActiveAsync(int id, bool active)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Barang SET IsActive = @Active, UpdatedAt = SYSDATETIME() WHERE BarangId = @Id",
            new { Id = id, Active = active });
    }

    /// <summary>Perubahan HET terpisah dari harga jual (BR-07/BR-19).</summary>
    public async Task UpdateHetAsync(int id, decimal het)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Barang SET HET = @Het, UpdatedAt = SYSDATETIME() WHERE BarangId = @Id AND IsSubsidi = 1",
            new { Id = id, Het = het });
    }

    // ===================== Perubahan stok (selalu di dalam transaksi) =====================

    /// <summary>Menambah stok dan memperbarui HPP rata-rata tertimbang (BR-14). Mengembalikan saldo baru.</summary>
    public Task<decimal?> IncreaseWithHppAsync(SqlConnection conn, SqlTransaction tx, int id, decimal jumlah, decimal harga)
        => conn.ScalarAsync<decimal?>(@"
UPDATE dbo.Barang SET
    HargaPokokRata = CASE WHEN StokSaatIni + @Jumlah <= 0 THEN HargaPokokRata
                          ELSE (StokSaatIni * HargaPokokRata + @Jumlah * @Harga) / (StokSaatIni + @Jumlah) END,
    StokSaatIni = StokSaatIni + @Jumlah,
    UpdatedAt = SYSDATETIME()
OUTPUT inserted.StokSaatIni
WHERE BarangId = @Id", new { Id = id, Jumlah = jumlah, Harga = harga }, tx);

    /// <summary>Menambah stok tanpa mengubah HPP (barang titipan, pembatalan penjualan, penyesuaian).</summary>
    public Task<decimal?> IncreaseAsync(SqlConnection conn, SqlTransaction tx, int id, decimal jumlah)
        => conn.ScalarAsync<decimal?>(@"
UPDATE dbo.Barang SET StokSaatIni = StokSaatIni + @Jumlah, UpdatedAt = SYSDATETIME()
OUTPUT inserted.StokSaatIni
WHERE BarangId = @Id", new { Id = id, Jumlah = jumlah }, tx);

    /// <summary>
    /// Mengurangi stok secara atomik (NFR-04): hanya berhasil bila stok mencukupi.
    /// Mengembalikan saldo baru, atau null bila stok tidak cukup.
    /// </summary>
    public Task<decimal?> DecreaseAsync(SqlConnection conn, SqlTransaction tx, int id, decimal jumlah)
        => conn.ScalarAsync<decimal?>(@"
UPDATE dbo.Barang SET StokSaatIni = StokSaatIni - @Jumlah, UpdatedAt = SYSDATETIME()
OUTPUT inserted.StokSaatIni
WHERE BarangId = @Id AND StokSaatIni >= @Jumlah", new { Id = id, Jumlah = jumlah }, tx);

    /// <summary>Membalik penerimaan: mengurangi stok dan mengembalikan HPP ke kondisi sebelum penerimaan.</summary>
    public Task<decimal?> DecreaseReverseHppAsync(SqlConnection conn, SqlTransaction tx, int id, decimal jumlah, decimal harga)
        => conn.ScalarAsync<decimal?>(@"
UPDATE dbo.Barang SET
    HargaPokokRata = CASE WHEN StokSaatIni - @Jumlah <= 0 THEN HargaPokokRata
                          WHEN (StokSaatIni * HargaPokokRata - @Jumlah * @Harga) < 0 THEN HargaPokokRata
                          ELSE (StokSaatIni * HargaPokokRata - @Jumlah * @Harga) / (StokSaatIni - @Jumlah) END,
    StokSaatIni = StokSaatIni - @Jumlah,
    UpdatedAt = SYSDATETIME()
OUTPUT inserted.StokSaatIni
WHERE BarangId = @Id AND StokSaatIni >= @Jumlah", new { Id = id, Jumlah = jumlah, Harga = harga }, tx);

    /// <summary>Mencatat satu baris mutasi stok (sumber kartu stok).</summary>
    public Task InsertMutasiAsync(SqlConnection conn, SqlTransaction tx, int barangId, DateTime tanggal, string jenis,
        string refTipe, int refId, string nomor, decimal masuk, decimal keluar, decimal saldo, string keterangan)
        => conn.ExecAsync(@"
INSERT INTO dbo.MutasiStok (BarangId, Tanggal, JenisMutasi, RefTipe, RefId, NomorDokumen, Masuk, Keluar, SaldoSetelah, Keterangan, UserId)
VALUES (@BarangId, @Tanggal, @Jenis, @RefTipe, @RefId, @Nomor, @Masuk, @Keluar, @Saldo, @Keterangan, @UserId)",
            new
            {
                BarangId = barangId, Tanggal = tanggal, Jenis = jenis, RefTipe = refTipe, RefId = refId, Nomor = nomor,
                Masuk = masuk, Keluar = keluar, Saldo = saldo, Keterangan = keterangan, UserId = Session.UserId
            }, tx);

    /// <summary>Kartu stok: saldo awal sebelum periode dan mutasi dalam periode (FR-15).</summary>
    public async Task<(decimal SaldoAwal, List<MutasiRow> Rows)> KartuStokAsync(int barangId, DateTime dari, DateTime sampai)
    {
        await using var conn = await _db.OpenAsync();
        var saldoAwal = await conn.ScalarAsync<decimal?>(@"
SELECT TOP 1 SaldoSetelah FROM dbo.MutasiStok
WHERE BarangId = @Id AND Tanggal < @Dari ORDER BY Tanggal DESC, MutasiId DESC",
            new { Id = barangId, Dari = dari.Date }) ?? 0m;
        var rows = await conn.QueryAsync(@"
SELECT m.Tanggal, m.JenisMutasi, m.NomorDokumen, m.Masuk, m.Keluar, m.SaldoSetelah, m.Keterangan, u.Nama AS Pengguna
FROM dbo.MutasiStok m JOIN dbo.Users u ON u.UserId = m.UserId
WHERE m.BarangId = @Id AND m.Tanggal >= @Dari AND m.Tanggal < @Sampai
ORDER BY m.Tanggal, m.MutasiId",
            r => new MutasiRow
            {
                Tanggal = r.Date("Tanggal"),
                JenisMutasi = r.Str("JenisMutasi"),
                NomorDokumen = r.Str("NomorDokumen"),
                Masuk = r.Dec("Masuk"),
                Keluar = r.Dec("Keluar"),
                SaldoSetelah = r.Dec("SaldoSetelah"),
                Keterangan = r.Str("Keterangan"),
                Pengguna = r.Str("Pengguna")
            }, new { Id = barangId, Dari = dari.Date, Sampai = sampai.Date.AddDays(1) });
        return (saldoAwal, rows);
    }
}
