using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Stokara.Infrastructure;

/// <summary>Pembuat koneksi SQL Server (DbConnectionFactory).</summary>
public sealed class Db
{
    public string ConnectionString { get; }

    public Db(string connectionString) => ConnectionString = connectionString;

    public async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Menjalankan pekerjaan dalam satu transaksi database (BR-17): commit bila sukses, rollback bila gagal.</summary>
    public async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        await using var conn = await OpenAsync();
        await using var tx = conn.BeginTransaction(isolation);
        try
        {
            var result = await work(conn, tx);
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { /* koneksi mungkin sudah putus */ }
            throw;
        }
    }

    public Task InTransactionAsync(Func<SqlConnection, SqlTransaction, Task> work)
        => InTransactionAsync<bool>(async (c, t) => { await work(c, t); return true; });

    public async Task<bool> PingAsync()
    {
        try
        {
            await using var conn = await OpenAsync();
            await using var cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = 3 };
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Helper ADO.NET. Semua query memakai parameter (NFR-05): argumen dikirim sebagai objek anonim
/// dan setiap properti menjadi parameter @NamaProperti.
/// </summary>
public static class SqlExt
{
    public static SqlCommand Command(this SqlConnection conn, string sql, object args = null, SqlTransaction tx = null)
    {
        var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 30 };
        if (args != null)
        {
            foreach (var p in args.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = p.GetValue(args);
                var param = cmd.Parameters.AddWithValue("@" + p.Name, value ?? DBNull.Value);
                if (value is decimal)
                {
                    param.SqlDbType = SqlDbType.Decimal;
                    param.Precision = 18;
                    param.Scale = 4;
                }
                else if (value is DateTime)
                {
                    param.SqlDbType = SqlDbType.DateTime2;
                }
            }
        }
        return cmd;
    }

    public static async Task<List<T>> QueryAsync<T>(this SqlConnection conn, string sql, Func<SqlDataReader, T> map,
        object args = null, SqlTransaction tx = null)
    {
        await using var cmd = conn.Command(sql, args, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync()) list.Add(map(reader));
        return list;
    }

    public static async Task<T> QuerySingleAsync<T>(this SqlConnection conn, string sql, Func<SqlDataReader, T> map,
        object args = null, SqlTransaction tx = null)
    {
        var list = await conn.QueryAsync(sql, map, args, tx);
        return list.Count > 0 ? list[0] : default;
    }

    public static async Task<int> ExecAsync(this SqlConnection conn, string sql, object args = null, SqlTransaction tx = null)
    {
        await using var cmd = conn.Command(sql, args, tx);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T> ScalarAsync<T>(this SqlConnection conn, string sql, object args = null, SqlTransaction tx = null)
    {
        await using var cmd = conn.Command(sql, args, tx);
        var v = await cmd.ExecuteScalarAsync();
        if (v == null || v is DBNull) return default;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(v, target);
    }

    public static async Task<DataTable> TableAsync(this SqlConnection conn, string sql, object args = null, SqlTransaction tx = null)
    {
        await using var cmd = conn.Command(sql, args, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    // ---- Pembacaan kolom ----
    public static string Str(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
    }

    public static int Int(this SqlDataReader r, string col) => Convert.ToInt32(r[col]);

    public static int? IntN(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    public static long Long(this SqlDataReader r, string col) => Convert.ToInt64(r[col]);

    public static decimal Dec(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }

    public static decimal? DecN(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
    }

    public static bool Bool(this SqlDataReader r, string col) => Convert.ToBoolean(r[col]);

    public static DateTime Date(this SqlDataReader r, string col) => Convert.ToDateTime(r[col]);

    public static DateTime? DateN(this SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i));
    }
}
