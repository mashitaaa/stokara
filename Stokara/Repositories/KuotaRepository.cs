using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Aturan kuota (Barang × Jenis Penerima × Periode) dan pemakaian per penerima (A-03).</summary>
public sealed class KuotaRepository
{
    private readonly Db _db;
    public KuotaRepository(Db db) => _db = db;

    public async Task<List<AturanKuota>> ListAturanAsync(string periode)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT a.AturanId, a.BarangId, b.Nama AS BarangNama, s.Nama AS SatuanNama, a.JenisPenerima, a.Periode, a.JumlahKuota,
       (SELECT COUNT(*) FROM dbo.PenerimaSubsidi p WHERE p.JenisPenerima = a.JenisPenerima AND p.Status = 'AKTIF') AS JumlahPenerima,
       ISNULL((SELECT SUM(pk.JumlahTerpakai) FROM dbo.PemakaianKuota pk
               JOIN dbo.PenerimaSubsidi p ON p.PenerimaId = pk.PenerimaId
               WHERE pk.BarangId = a.BarangId AND pk.Periode = a.Periode AND p.JenisPenerima = a.JenisPenerima), 0) AS TotalTerpakai
FROM dbo.AturanKuota a
JOIN dbo.Barang b ON b.BarangId = a.BarangId
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE (@Periode IS NULL OR a.Periode = @Periode)
ORDER BY a.Periode DESC, b.Nama, a.JenisPenerima",
            r => new AturanKuota
            {
                AturanId = r.Int("AturanId"),
                BarangId = r.Int("BarangId"),
                BarangNama = r.Str("BarangNama"),
                SatuanNama = r.Str("SatuanNama"),
                JenisPenerima = r.Str("JenisPenerima"),
                Periode = r.Str("Periode"),
                JumlahKuota = r.Dec("JumlahKuota"),
                JumlahPenerima = r.Int("JumlahPenerima"),
                TotalTerpakai = r.Dec("TotalTerpakai")
            }, new { Periode = periode });
    }

    public async Task<bool> AturanExistsAsync(int barangId, string jenis, string periode, int excludeId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>(@"
SELECT COUNT(*) FROM dbo.AturanKuota
WHERE BarangId = @BarangId AND JenisPenerima = @Jenis AND Periode = @Periode AND AturanId <> @Id",
            new { BarangId = barangId, Jenis = jenis, Periode = periode, Id = excludeId }) > 0;
    }

    public async Task<int> SaveAturanAsync(AturanKuota a)
    {
        var args = new { a.AturanId, a.BarangId, a.JenisPenerima, a.Periode, a.JumlahKuota };
        await using var conn = await _db.OpenAsync();
        if (a.AturanId == 0)
            return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.AturanKuota (BarangId, JenisPenerima, Periode, JumlahKuota) OUTPUT inserted.AturanId
VALUES (@BarangId, @JenisPenerima, @Periode, @JumlahKuota)", args);
        await conn.ExecAsync(@"
UPDATE dbo.AturanKuota SET BarangId = @BarangId, JenisPenerima = @JenisPenerima, Periode = @Periode, JumlahKuota = @JumlahKuota
WHERE AturanId = @AturanId", args);
        return a.AturanId;
    }

    public async Task DeleteAturanAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("DELETE FROM dbo.AturanKuota WHERE AturanId = @Id", new { Id = id });
    }

    /// <summary>Menyalin aturan dari periode sebelumnya ke periode baru (yang belum ada).</summary>
    public async Task<int> CopyAturanAsync(string dariPeriode, string kePeriode)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecAsync(@"
INSERT INTO dbo.AturanKuota (BarangId, JenisPenerima, Periode, JumlahKuota)
SELECT a.BarangId, a.JenisPenerima, @Ke, a.JumlahKuota FROM dbo.AturanKuota a
WHERE a.Periode = @Dari AND NOT EXISTS (
    SELECT 1 FROM dbo.AturanKuota x WHERE x.BarangId = a.BarangId AND x.JenisPenerima = a.JenisPenerima AND x.Periode = @Ke)",
            new { Dari = dariPeriode, Ke = kePeriode });
    }

    /// <summary>Sisa kuota setiap barang subsidi untuk satu penerima pada satu periode.</summary>
    public async Task<List<SisaKuota>> SisaKuotaAsync(int penerimaId, string periode)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT a.BarangId, b.Nama AS BarangNama, s.Nama AS SatuanNama, a.JumlahKuota AS Kuota,
       ISNULL(pk.JumlahTerpakai, 0) AS Terpakai
FROM dbo.PenerimaSubsidi p
JOIN dbo.AturanKuota a ON a.JenisPenerima = p.JenisPenerima AND a.Periode = @Periode
JOIN dbo.Barang b ON b.BarangId = a.BarangId AND b.IsActive = 1
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
LEFT JOIN dbo.PemakaianKuota pk ON pk.PenerimaId = p.PenerimaId AND pk.BarangId = a.BarangId AND pk.Periode = a.Periode
WHERE p.PenerimaId = @PenerimaId
ORDER BY b.Nama",
            r => new SisaKuota
            {
                BarangId = r.Int("BarangId"),
                BarangNama = r.Str("BarangNama"),
                SatuanNama = r.Str("SatuanNama"),
                Kuota = r.Dec("Kuota"),
                Terpakai = r.Dec("Terpakai")
            }, new { PenerimaId = penerimaId, Periode = periode });
    }

    // ============ Operasi di dalam transaksi penyaluran ============

    public Task<decimal?> GetKuotaAsync(SqlConnection conn, SqlTransaction tx, int barangId, string jenisPenerima, string periode)
        => conn.ScalarAsync<decimal?>(@"
SELECT JumlahKuota FROM dbo.AturanKuota
WHERE BarangId = @BarangId AND JenisPenerima = @Jenis AND Periode = @Periode",
            new { BarangId = barangId, Jenis = jenisPenerima, Periode = periode }, tx);

    public async Task<decimal> GetTerpakaiAsync(SqlConnection conn, SqlTransaction tx, int penerimaId, int barangId, string periode)
        => await conn.ScalarAsync<decimal?>(@"
SELECT JumlahTerpakai FROM dbo.PemakaianKuota
WHERE PenerimaId = @PenerimaId AND BarangId = @BarangId AND Periode = @Periode",
            new { PenerimaId = penerimaId, BarangId = barangId, Periode = periode }, tx) ?? 0m;

    /// <summary>
    /// Menambah pemakaian kuota secara atomik (NFR-04). Baris pemakaian dibuat otomatis bila belum ada (A-03).
    /// Mengembalikan false bila jumlah melebihi sisa kuota.
    /// </summary>
    public async Task<bool> TryConsumeAsync(SqlConnection conn, SqlTransaction tx, int penerimaId, int barangId,
        string periode, decimal jumlah, decimal kuota)
    {
        var args = new { PenerimaId = penerimaId, BarangId = barangId, Periode = periode, Jumlah = jumlah, Kuota = kuota };
        await conn.ExecAsync(@"
IF NOT EXISTS (SELECT 1 FROM dbo.PemakaianKuota WITH (UPDLOCK, HOLDLOCK)
               WHERE PenerimaId = @PenerimaId AND BarangId = @BarangId AND Periode = @Periode)
    INSERT INTO dbo.PemakaianKuota (PenerimaId, BarangId, Periode, JumlahTerpakai) VALUES (@PenerimaId, @BarangId, @Periode, 0);",
            args, tx);
        var affected = await conn.ExecAsync(@"
UPDATE dbo.PemakaianKuota SET JumlahTerpakai = JumlahTerpakai + @Jumlah
WHERE PenerimaId = @PenerimaId AND BarangId = @BarangId AND Periode = @Periode
  AND JumlahTerpakai + @Jumlah <= @Kuota", args, tx);
        return affected == 1;
    }

    /// <summary>Memulihkan kuota saat penyaluran dibatalkan (BR-09).</summary>
    public Task RestoreAsync(SqlConnection conn, SqlTransaction tx, int penerimaId, int barangId, string periode, decimal jumlah)
        => conn.ExecAsync(@"
UPDATE dbo.PemakaianKuota SET JumlahTerpakai = CASE WHEN JumlahTerpakai >= @Jumlah THEN JumlahTerpakai - @Jumlah ELSE 0 END
WHERE PenerimaId = @PenerimaId AND BarangId = @BarangId AND Periode = @Periode",
            new { PenerimaId = penerimaId, BarangId = barangId, Periode = periode, Jumlah = jumlah }, tx);
}
