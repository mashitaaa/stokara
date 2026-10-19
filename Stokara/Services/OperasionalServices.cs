using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

/// <summary>Hutang penitip dan pelunasan FIFO (FR-24-26).</summary>
public sealed class KonsinyasiService
{
    private readonly Db _db;
    private readonly KonsinyasiRepository _repo;
    private readonly TransaksiRepository _trx;
    private readonly AktivitasRepository _aktivitas;

    public KonsinyasiService(Db db, KonsinyasiRepository repo, TransaksiRepository trx, AktivitasRepository aktivitas)
    {
        _db = db;
        _repo = repo;
        _trx = trx;
        _aktivitas = aktivitas;
    }

    public Task<List<PenitipRingkas>> RingkasanAsync() => _repo.RingkasanAsync();
    public Task<List<HutangRow>> ListHutangAsync(int penitipId, bool belumLunas) => _repo.ListHutangAsync(penitipId, belumLunas);
    public Task<List<PelunasanRow>> ListPelunasanAsync(int? penitipId) => _repo.ListPelunasanAsync(penitipId);

    /// <summary>Mencatat pembayaran ke penitip; dialokasikan ke hutang tertua lebih dulu (A-07, BR-13).</summary>
    public async Task<string> BayarAsync(int penitipId, decimal jumlah, string catatan)
    {
        Session.Require("mencatat pelunasan penitip", Roles.Manajer, Roles.Pengurus);
        if (jumlah <= 0) throw new BusinessException("Jumlah pembayaran harus lebih dari 0.");

        return await _db.InTransactionAsync(async (conn, tx) =>
        {
            var debts = await _repo.OpenDebtsForUpdateAsync(conn, tx, penitipId);
            var sisa = debts.Sum(d => d.NilaiHutang - d.JumlahDibayar);
            if (sisa <= 0) throw new BusinessException("Penitip ini tidak memiliki hutang yang belum lunas.");
            if (jumlah > sisa) throw new BusinessException($"Pembayaran melebihi sisa hutang ({Fmt.Rp(sisa)}).");

            var nomor = await _trx.NextNumberAsync(conn, tx, "PL", DateTime.Now);
            var pelunasanId = await _repo.InsertPelunasanAsync(conn, tx, nomor, penitipId, jumlah,
                string.IsNullOrWhiteSpace(catatan) ? null : catatan.Trim());

            var remaining = jumlah;
            foreach (var d in debts)
            {
                if (remaining <= 0) break;
                var bayar = Math.Min(remaining, d.NilaiHutang - d.JumlahDibayar);
                if (bayar <= 0) continue;
                await _repo.AllocateAsync(conn, tx, pelunasanId, d.HutangId, bayar);
                remaining -= bayar;
            }
            await _aktivitas.InsertAsync(Session.UserId, "PELUNASAN", "PelunasanPenitip", nomor,
                $"Penitip {penitipId}, {jumlah:N0}", conn, tx);
            return nomor;
        });
    }
}

/// <summary>Stok opname: snapshot, input fisik, pengajuan, persetujuan oleh orang lain (FR-16, BR-15).</summary>
public sealed class OpnameService
{
    private readonly Db _db;
    private readonly OpnameRepository _repo;
    private readonly BarangRepository _barang;
    private readonly TransaksiRepository _trx;
    private readonly AktivitasRepository _aktivitas;

    public OpnameService(Db db, OpnameRepository repo, BarangRepository barang, TransaksiRepository trx, AktivitasRepository aktivitas)
    {
        _db = db;
        _repo = repo;
        _barang = barang;
        _trx = trx;
        _aktivitas = aktivitas;
    }

    public Task<List<OpnameRow>> ListAsync() => _repo.ListAsync();
    public Task<OpnameRow> GetAsync(int id) => _repo.GetAsync(id);
    public Task<List<OpnameDetailRow>> DetailAsync(int id) => _repo.DetailAsync(id);

    public async Task<int> CreateAsync(int? kategoriId, string catatan)
    {
        Session.Require("membuat sesi stok opname", Roles.Manajer, Roles.Gudang);
        return await _db.InTransactionAsync(async (conn, tx) =>
        {
            var nomor = await _trx.NextNumberAsync(conn, tx, "OP", DateTime.Now);
            var id = await _repo.InsertAsync(conn, tx, nomor, kategoriId, string.IsNullOrWhiteSpace(catatan) ? null : catatan.Trim());
            var n = await _repo.SnapshotAsync(conn, tx, id, kategoriId);
            if (n == 0) throw new BusinessException("Tidak ada barang aktif pada kategori ini.");
            await _aktivitas.InsertAsync(Session.UserId, "OPNAME_BUAT", "StokOpname", nomor, $"{n} barang", conn, tx);
            return id;
        });
    }

    public async Task SaveCountsAsync(int opnameId, IEnumerable<OpnameDetailRow> rows)
    {
        Session.Require("mengisi hitung fisik", Roles.Manajer, Roles.Gudang);
        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var status = await _repo.GetStatusForUpdateAsync(conn, tx, opnameId);
            if (status != "DRAFT") throw new BusinessException("Hanya sesi berstatus Draft yang dapat diubah.");
            foreach (var r in rows)
            {
                if (r.StokFisik < 0) throw new BusinessException($"Stok fisik {r.BarangNama} tidak boleh negatif.");
                await _repo.UpdateDetailAsync(conn, tx, r.DetailId, r.StokFisik, r.Alasan);
            }
        });
    }

    public async Task SubmitAsync(int opnameId, IEnumerable<OpnameDetailRow> rows)
    {
        Session.Require("mengajukan penyesuaian opname", Roles.Manajer, Roles.Gudang);
        var list = rows.ToList();
        if (list.Any(r => !r.StokFisik.HasValue))
            throw new BusinessException("Masih ada barang yang belum dihitung. Isi semua stok fisik sebelum mengajukan.");
        var tanpaAlasan = list.FirstOrDefault(r => r.Selisih != 0 && string.IsNullOrWhiteSpace(r.Alasan));
        if (tanpaAlasan != null)
            throw new BusinessException($"Isi alasan selisih untuk {tanpaAlasan.BarangNama} (BR-15).");

        await SaveCountsAsync(opnameId, list);
        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var status = await _repo.GetStatusForUpdateAsync(conn, tx, opnameId);
            if (status != "DRAFT") throw new BusinessException("Sesi ini sudah diajukan.");
            await _repo.SetStatusAsync(conn, tx, opnameId, "DIAJUKAN");
            await _aktivitas.InsertAsync(Session.UserId, "OPNAME_AJUKAN", "StokOpname", opnameId.ToString(),
                $"{list.Count(r => r.Selisih != 0)} barang berselisih", conn, tx);
        });
    }

    /// <summary>
    /// Persetujuan: penyesuaian = selisih (fisik − sistem saat snapshot) diterapkan ke stok saat ini,
    /// sehingga transaksi yang terjadi setelah snapshot tetap dihitung.
    /// </summary>
    public async Task ApproveAsync(int opnameId)
    {
        Session.Require("menyetujui penyesuaian opname", Roles.Manajer, Roles.Pengurus);
        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var status = await _repo.GetStatusForUpdateAsync(conn, tx, opnameId);
            if (status != "DIAJUKAN") throw new BusinessException("Hanya sesi berstatus Diajukan yang dapat disetujui.");
            var header = await _repo.GetAsync(opnameId, conn, tx);
            if (header.DiajukanOlehId == Session.UserId)
                throw new BusinessException("Penyesuaian harus disetujui oleh orang lain, bukan pengaju (BR-15).");

            var now = DateTime.Now;
            foreach (var d in (await _repo.DetailAsync(opnameId, conn, tx)).Where(x => x.Selisih is decimal s && s != 0))
            {
                var selisih = d.Selisih.Value;
                decimal? saldo = selisih > 0
                    ? await _barang.IncreaseAsync(conn, tx, d.BarangId, selisih)
                    : await _barang.DecreaseAsync(conn, tx, d.BarangId, -selisih);
                if (saldo == null)
                    throw new BusinessException($"Penyesuaian {d.BarangNama} membuat stok negatif karena ada penjualan setelah opname. " +
                                                "Tolak sesi ini dan buat opname baru.");
                await _barang.InsertMutasiAsync(conn, tx, d.BarangId, now, "PENYESUAIAN", "OPNAME", opnameId, header.NomorDokumen,
                    selisih > 0 ? selisih : 0, selisih < 0 ? -selisih : 0, saldo.Value, d.Alasan);
            }
            await _repo.SetStatusAsync(conn, tx, opnameId, "DISETUJUI");
            await _aktivitas.InsertAsync(Session.UserId, "OPNAME_SETUJU", "StokOpname", header.NomorDokumen, null, conn, tx);
        });
    }

    public async Task RejectAsync(int opnameId, string alasan)
    {
        Session.Require("menolak penyesuaian opname", Roles.Manajer, Roles.Pengurus);
        if (string.IsNullOrWhiteSpace(alasan)) throw new BusinessException("Alasan penolakan wajib diisi.");
        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var status = await _repo.GetStatusForUpdateAsync(conn, tx, opnameId);
            if (status != "DIAJUKAN") throw new BusinessException("Hanya sesi berstatus Diajukan yang dapat ditolak.");
            var header = await _repo.GetAsync(opnameId, conn, tx);
            if (header.DiajukanOlehId == Session.UserId)
                throw new BusinessException("Penolakan harus dilakukan oleh orang lain, bukan pengaju.");
            await _repo.SetStatusAsync(conn, tx, opnameId, "DITOLAK", alasan.Trim());
            await _aktivitas.InsertAsync(Session.UserId, "OPNAME_TOLAK", "StokOpname", header.NomorDokumen, alasan.Trim(), conn, tx);
        });
    }
}

/// <summary>Backup dan restore database manual (FR-33, NFR-12).</summary>
public sealed class BackupService
{
    private readonly Db _db;
    private readonly ActivityService _log;

    public BackupService(Db db, ActivityService log)
    {
        _db = db;
        _log = log;
    }

    private string DatabaseName => new SqlConnectionStringBuilder(_db.ConnectionString).InitialCatalog;

    public async Task BackupAsync(string path)
    {
        Session.Require("mencadangkan database", Roles.Manajer);
        await using var conn = await _db.OpenAsync();
        await using var cmd = conn.Command($"BACKUP DATABASE [{DatabaseName}] TO DISK = @Path WITH INIT, FORMAT, NAME = N'STOKARA backup'",
            new { Path = path });
        cmd.CommandTimeout = 600;
        await cmd.ExecuteNonQueryAsync();
        await _log.LogAsync("BACKUP", "Database", null, Path.GetFileName(path));
    }

    /// <summary>Memulihkan database dari file .bak. Semua koneksi lain diputus; pengguna harus login ulang.</summary>
    public async Task RestoreAsync(string path)
    {
        Session.Require("memulihkan database", Roles.Manajer);
        var name = DatabaseName;
        var master = new SqlConnectionStringBuilder(_db.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        SqlConnection.ClearAllPools();
        await using var conn = new SqlConnection(master);
        await conn.OpenAsync();
        await using var cmd = conn.Command($@"
ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [{name}] FROM DISK = @Path WITH REPLACE;
ALTER DATABASE [{name}] SET MULTI_USER;", new { Path = path });
        cmd.CommandTimeout = 900;
        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            try
            {
                await using var reset = conn.Command($"ALTER DATABASE [{name}] SET MULTI_USER;");
                await reset.ExecuteNonQueryAsync();
            }
            catch { /* abaikan */ }
            throw;
        }
        SqlConnection.ClearAllPools();
        Logger.Info($"Database dipulihkan dari {path}");
    }
}
