using System.Text.RegularExpressions;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

/// <summary>Aturan master barang (FR-08-10, A-01, A-02, A-05, A-16, BR-07).</summary>
public sealed class BarangService
{
    private readonly BarangRepository _repo;
    private readonly ActivityService _log;

    public BarangService(BarangRepository repo, ActivityService log)
    {
        _repo = repo;
        _log = log;
    }

    public Task<(List<Barang> Items, int Total)> SearchAsync(BarangFilter f) => _repo.SearchAsync(f);
    public Task<List<Barang>> QuickSearchAsync(string kw, string jenis = null, int? penitipId = null) => _repo.QuickSearchAsync(kw, jenis, penitipId);
    public Task<Barang> GetByCodeAsync(string code) => _repo.GetByCodeAsync(code);
    public Task<Barang> GetAsync(int id) => _repo.GetAsync(id);
    public Task<List<Barang>> ListAsync(string jenis = null, int? penitipId = null) => _repo.ListAsync(jenis, penitipId);

    public Task<(decimal SaldoAwal, List<MutasiRow> Rows)> KartuStokAsync(int barangId, DateTime dari, DateTime sampai)
        => _repo.KartuStokAsync(barangId, dari, sampai);

    /// <summary>Daftar pesan validasi per field; kosong bila valid. Dipakai juga oleh form untuk validasi inline.</summary>
    public static Dictionary<string, string> Validate(Barang b)
    {
        var e = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(b.SKU)) e[nameof(Barang.SKU)] = "SKU wajib diisi.";
        else if (b.SKU.Length > 30) e[nameof(Barang.SKU)] = "SKU maksimal 30 karakter.";
        if (string.IsNullOrWhiteSpace(b.Nama)) e[nameof(Barang.Nama)] = "Nama barang wajib diisi.";
        if (!string.IsNullOrWhiteSpace(b.Barcode) && !Regex.IsMatch(b.Barcode.Trim(), "^[0-9A-Za-z-]{4,50}$"))
            e[nameof(Barang.Barcode)] = "Barcode hanya huruf/angka, 4 sampai 50 karakter.";
        if (b.KategoriId <= 0) e[nameof(Barang.KategoriId)] = "Pilih kategori.";
        if (b.SatuanId <= 0) e[nameof(Barang.SatuanId)] = "Pilih satuan.";
        if (b.HargaUmum < 0) e[nameof(Barang.HargaUmum)] = "Harga tidak boleh negatif.";
        if (b.HargaAnggota < 0) e[nameof(Barang.HargaAnggota)] = "Harga tidak boleh negatif.";
        if (b.HargaAnggota > b.HargaUmum && !b.IsSubsidi)
            e[nameof(Barang.HargaAnggota)] = "Harga anggota tidak boleh lebih mahal dari harga umum.";
        if (b.StokMinimum < 0) e[nameof(Barang.StokMinimum)] = "Stok minimum tidak boleh negatif.";

        if (b.IsSubsidi)
        {
            if (b.IsTitipan) e[nameof(Barang.Kepemilikan)] = "Barang subsidi harus milik koperasi, bukan titipan.";
            if (!b.HET.HasValue || b.HET <= 0) e[nameof(Barang.HET)] = "Barang subsidi wajib memiliki HET.";
            else if (b.HargaUmum > b.HET) e[nameof(Barang.HargaUmum)] = $"Harga melebihi HET ({Fmt.Rp(b.HET.Value)}).";
        }
        if (b.IsTitipan)
        {
            if (!b.PenitipId.HasValue) e[nameof(Barang.PenitipId)] = "Barang titipan wajib memiliki penitip.";
            if (!b.HargaTitip.HasValue || b.HargaTitip < 0) e[nameof(Barang.HargaTitip)] = "Isi harga titip.";
            else if (b.HargaTitip > b.HargaUmum || b.HargaTitip > b.HargaAnggota)
                e[nameof(Barang.HargaTitip)] = "Harga titip tidak boleh melebihi harga jual.";
        }
        return e;
    }

    public async Task<int> SaveAsync(Barang b)
    {
        Session.Require("mengubah data barang", Roles.Manajer, Roles.Gudang);
        b.SKU = b.SKU?.Trim().ToUpperInvariant();
        b.Nama = b.Nama?.Trim();
        b.Barcode = string.IsNullOrWhiteSpace(b.Barcode) ? null : b.Barcode.Trim();
        if (b.IsSubsidi) b.HargaAnggota = b.HargaUmum;   // A-02: satu harga untuk barang subsidi

        var errors = Validate(b);
        if (errors.Count > 0) throw new BusinessException(errors.Values.First());
        if (await _repo.ExistsAsync("SKU", b.SKU, b.BarangId)) throw new BusinessException($"SKU {b.SKU} sudah dipakai barang lain.");
        if (b.Barcode != null && await _repo.ExistsAsync("Barcode", b.Barcode, b.BarangId))
            throw new BusinessException($"Barcode {b.Barcode} sudah dipakai barang lain.");

        // A-16: Petugas Gudang tidak boleh menetapkan/mengubah harga, HET, dan harga titip.
        if (Session.Is(Roles.Gudang))
        {
            if (b.BarangId == 0)
            {
                if (b.IsSubsidi || b.IsTitipan)
                    throw new AccessDeniedException("membuat barang subsidi/titipan (harus oleh Manajer)");
                if (b.HargaUmum != 0 || b.HargaAnggota != 0)
                    throw new AccessDeniedException("menetapkan harga jual");
            }
            else
            {
                var old = await _repo.GetAsync(b.BarangId);
                if (old.HargaUmum != b.HargaUmum || old.HargaAnggota != b.HargaAnggota || old.HET != b.HET ||
                    old.HargaTitip != b.HargaTitip || old.IsSubsidi != b.IsSubsidi || old.Kepemilikan != b.Kepemilikan ||
                    old.PenitipId != b.PenitipId)
                    throw new AccessDeniedException("mengubah harga, HET, harga titip, atau jenis barang");
            }
        }

        if (b.BarangId == 0)
        {
            var id = await _repo.InsertAsync(b);
            await _log.LogAsync("TAMBAH", "Barang", id, $"{b.SKU} {b.Nama}");
            return id;
        }

        var before = await _repo.GetAsync(b.BarangId);
        await _repo.UpdateAsync(b);
        var changes = new List<string>();
        if (before.HargaUmum != b.HargaUmum) changes.Add($"harga umum {before.HargaUmum:N0}→{b.HargaUmum:N0}");
        if (before.HargaAnggota != b.HargaAnggota) changes.Add($"harga anggota {before.HargaAnggota:N0}→{b.HargaAnggota:N0}");
        if (before.HET != b.HET) changes.Add($"HET {before.HET:N0}→{b.HET:N0}");
        if (before.HargaTitip != b.HargaTitip) changes.Add($"harga titip {before.HargaTitip:N0}→{b.HargaTitip:N0}");
        await _log.LogAsync(before.HET != b.HET ? "UBAH_HET" : "UBAH", "Barang", b.BarangId,
            $"{b.SKU} {b.Nama}" + (changes.Count > 0 ? " · " + string.Join(", ", changes) : ""));
        return b.BarangId;
    }

    public async Task SetActiveAsync(Barang b, bool active)
    {
        Session.Require("menonaktifkan barang", Roles.Manajer);
        await _repo.SetActiveAsync(b.BarangId, active);
        await _log.LogAsync(active ? "AKTIFKAN" : "NONAKTIFKAN", "Barang", b.BarangId, b.Nama);
    }

    /// <summary>Perubahan HET dari menu Parameter. Bila HET turun di bawah harga jual, penyaluran diblokir (BR-07).</summary>
    public async Task UpdateHetAsync(Barang b, decimal het)
    {
        Session.Require("mengubah HET", Roles.Manajer);
        if (het <= 0) throw new BusinessException("HET harus lebih dari 0.");
        await _repo.UpdateHetAsync(b.BarangId, het);
        await _log.LogAsync("UBAH_HET", "Barang", b.BarangId, $"{b.Nama}: HET {b.HET:N0}→{het:N0}");
    }
}

/// <summary>Kategori, satuan, pemasok, penitip, anggota, penerima, aturan kuota (FR-11, FR-12).</summary>
public sealed class MitraService
{
    private readonly MasterRepository _master;
    private readonly MitraRepository _mitra;
    private readonly KuotaRepository _kuota;
    private readonly ActivityService _log;

    public MitraService(MasterRepository master, MitraRepository mitra, KuotaRepository kuota, ActivityService log)
    {
        _master = master;
        _mitra = mitra;
        _kuota = kuota;
        _log = log;
    }

    private static readonly Regex NikRegex = new("^[0-9]{16}$");

    // ---------- Kategori / Satuan ----------
    public Task<List<LookupItem>> ListLookupAsync(string kind, bool activeOnly = true) => _master.ListLookupAsync(kind, activeOnly);

    public async Task SaveLookupAsync(string kind, LookupItem item)
    {
        Session.Require($"mengelola {kind.ToLower()}", Roles.Manajer);
        item.Nama = item.Nama?.Trim();
        if (string.IsNullOrWhiteSpace(item.Nama)) throw new BusinessException($"Nama {kind.ToLower()} wajib diisi.");
        if (await _master.LookupExistsAsync(kind, item.Nama, item.Id))
            throw new BusinessException($"{kind} '{item.Nama}' sudah ada.");
        var id = await _master.SaveLookupAsync(kind, item);
        await _log.LogAsync(item.Id == 0 ? "TAMBAH" : "UBAH", kind, id, item.Nama);
    }

    public async Task SetLookupActiveAsync(string kind, LookupItem item, bool active)
    {
        Session.Require($"mengelola {kind.ToLower()}", Roles.Manajer);
        await _master.SetLookupActiveAsync(kind, item.Id, active);
        await _log.LogAsync(active ? "AKTIFKAN" : "NONAKTIFKAN", kind, item.Id, item.Nama);
    }

    // ---------- Pemasok ----------
    public Task<List<Pemasok>> ListPemasokAsync(string kw = null, bool activeOnly = false) => _master.ListPemasokAsync(kw, activeOnly);

    public async Task SavePemasokAsync(Pemasok p)
    {
        Session.Require("mengelola pemasok", Roles.Manajer);
        p.Kode = p.Kode?.Trim().ToUpperInvariant();
        p.Nama = p.Nama?.Trim();
        if (string.IsNullOrWhiteSpace(p.Kode) || string.IsNullOrWhiteSpace(p.Nama))
            throw new BusinessException("Kode dan nama pemasok wajib diisi.");
        if (await _master.PemasokKodeExistsAsync(p.Kode, p.PemasokId))
            throw new BusinessException($"Kode pemasok {p.Kode} sudah dipakai.");
        var id = await _master.SavePemasokAsync(p);
        await _log.LogAsync(p.PemasokId == 0 ? "TAMBAH" : "UBAH", "Pemasok", id, p.Nama);
    }

    public async Task SetPemasokActiveAsync(Pemasok p, bool active)
    {
        Session.Require("mengelola pemasok", Roles.Manajer);
        await _master.SetPemasokActiveAsync(p.PemasokId, active);
        await _log.LogAsync(active ? "AKTIFKAN" : "NONAKTIFKAN", "Pemasok", p.PemasokId, p.Nama);
    }

    // ---------- Penitip ----------
    public Task<List<Penitip>> ListPenitipAsync(string kw = null, bool activeOnly = false) => _master.ListPenitipAsync(kw, activeOnly);

    public async Task SavePenitipAsync(Penitip p)
    {
        Session.Require("mengelola penitip", Roles.Manajer);
        p.Nama = p.Nama?.Trim();
        if (string.IsNullOrWhiteSpace(p.Nama)) throw new BusinessException("Nama penitip wajib diisi.");
        var id = await _master.SavePenitipAsync(p);
        await _log.LogAsync(p.PenitipId == 0 ? "TAMBAH" : "UBAH", "Penitip", id, p.Nama);
    }

    public async Task SetPenitipActiveAsync(Penitip p, bool active)
    {
        Session.Require("mengelola penitip", Roles.Manajer);
        await _master.SetPenitipActiveAsync(p.PenitipId, active);
        await _log.LogAsync(active ? "AKTIFKAN" : "NONAKTIFKAN", "Penitip", p.PenitipId, p.Nama);
    }

    // ---------- Anggota ----------
    public Task<List<Anggota>> ListAnggotaAsync(string kw = null, bool activeOnly = false) => _mitra.ListAnggotaAsync(kw, activeOnly);
    public Task<string> NextNomorAnggotaAsync() => _mitra.NextNomorAnggotaAsync();

    public async Task SaveAnggotaAsync(Anggota a)
    {
        // Kasir boleh menambah anggota baru, tetapi tidak mengubah data yang ada (matriks 4.1).
        if (a.AnggotaId == 0) Session.Require("menambah anggota", Roles.Manajer, Roles.Kasir);
        else Session.Require("mengubah data anggota", Roles.Manajer);

        a.Nama = a.Nama?.Trim();
        a.NIK = a.NIK?.Trim();
        a.NomorAnggota = a.NomorAnggota?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(a.Nama)) throw new BusinessException("Nama anggota wajib diisi.");
        if (string.IsNullOrWhiteSpace(a.NomorAnggota)) throw new BusinessException("Nomor anggota wajib diisi.");
        if (a.NIK == null || !NikRegex.IsMatch(a.NIK)) throw new BusinessException("NIK harus 16 digit angka.");
        if (await _mitra.AnggotaExistsAsync("NIK", a.NIK, a.AnggotaId)) throw new BusinessException("NIK sudah terdaftar sebagai anggota.");
        if (await _mitra.AnggotaExistsAsync("NomorAnggota", a.NomorAnggota, a.AnggotaId))
            throw new BusinessException($"Nomor anggota {a.NomorAnggota} sudah dipakai.");
        var id = await _mitra.SaveAnggotaAsync(a);
        await _log.LogAsync(a.AnggotaId == 0 ? "TAMBAH" : "UBAH", "Anggota", id, $"{a.NomorAnggota} {a.Nama}");
    }

    // ---------- Penerima subsidi ----------
    public Task<List<PenerimaSubsidi>> ListPenerimaAsync(string kw = null, bool activeOnly = false) => _mitra.ListPenerimaAsync(kw, activeOnly);
    public Task<PenerimaSubsidi> GetPenerimaAsync(int id) => _mitra.GetPenerimaAsync(id);

    public async Task SavePenerimaAsync(PenerimaSubsidi p)
    {
        Session.Require("mengelola penerima subsidi", Roles.Manajer);
        p.Nama = p.Nama?.Trim();
        p.NIK = p.NIK?.Trim();
        if (string.IsNullOrWhiteSpace(p.Nama)) throw new BusinessException("Nama penerima wajib diisi.");
        if (p.NIK == null || !NikRegex.IsMatch(p.NIK)) throw new BusinessException("NIK harus 16 digit angka.");
        if (await _mitra.PenerimaNikExistsAsync(p.NIK, p.PenerimaId))
            throw new BusinessException("NIK sudah terdaftar sebagai penerima subsidi.");
        var id = await _mitra.SavePenerimaAsync(p);
        await _log.LogAsync(p.PenerimaId == 0 ? "TAMBAH" : "UBAH", "PenerimaSubsidi", id, $"{p.Nama} ({p.JenisPenerima}, {p.Status})");
    }

    // ---------- Kuota ----------
    public Task<List<AturanKuota>> ListAturanAsync(string periode) => _kuota.ListAturanAsync(periode);
    public Task<List<SisaKuota>> SisaKuotaAsync(int penerimaId, string periode) => _kuota.SisaKuotaAsync(penerimaId, periode);

    public static bool IsValidPeriode(string p) => p != null && Regex.IsMatch(p, @"^\d{4}(-(0[1-9]|1[0-2]))?$");

    public async Task SaveAturanAsync(AturanKuota a)
    {
        Session.Require("mengatur kuota subsidi", Roles.Manajer);
        if (a.BarangId <= 0) throw new BusinessException("Pilih barang subsidi.");
        if (string.IsNullOrWhiteSpace(a.JenisPenerima)) throw new BusinessException("Pilih jenis penerima.");
        if (!IsValidPeriode(a.Periode)) throw new BusinessException("Periode harus berformat yyyy-MM (bulanan) atau yyyy (tahunan).");
        if (a.JumlahKuota <= 0) throw new BusinessException("Jumlah kuota harus lebih dari 0.");
        if (await _kuota.AturanExistsAsync(a.BarangId, a.JenisPenerima, a.Periode, a.AturanId))
            throw new BusinessException("Aturan kuota untuk barang, jenis penerima, dan periode ini sudah ada.");
        var id = await _kuota.SaveAturanAsync(a);
        await _log.LogAsync("UBAH_KUOTA", "AturanKuota", id,
            $"Barang {a.BarangId} · {a.JenisPenerima} · {a.Periode} = {a.JumlahKuota:N0}");
    }

    public async Task DeleteAturanAsync(AturanKuota a)
    {
        Session.Require("mengatur kuota subsidi", Roles.Manajer);
        if (a.TotalTerpakai > 0) throw new BusinessException("Aturan kuota yang sudah dipakai tidak dapat dihapus. Ubah jumlahnya saja.");
        await _kuota.DeleteAturanAsync(a.AturanId);
        await _log.LogAsync("HAPUS_KUOTA", "AturanKuota", a.AturanId, $"{a.BarangNama} · {a.JenisPenerima} · {a.Periode}");
    }

    public async Task<int> CopyAturanAsync(string dari, string ke)
    {
        Session.Require("mengatur kuota subsidi", Roles.Manajer);
        if (!IsValidPeriode(dari) || !IsValidPeriode(ke)) throw new BusinessException("Format periode tidak valid.");
        var n = await _kuota.CopyAturanAsync(dari, ke);
        await _log.LogAsync("SALIN_KUOTA", "AturanKuota", null, $"{dari} → {ke}: {n} aturan");
        return n;
    }
}
