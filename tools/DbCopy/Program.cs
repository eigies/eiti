// Copia datos SQL Server -> PostgreSQL tabla por tabla, usando el mismo esquema (generado por EF).
// Uso:  dotnet run --project tools/DbCopy -- "<sqlserver-conn>" "<postgres-conn>"
// - Fuente: Windows auth (Trusted_Connection) contra el SQLEXPRESS local.
// - Destino: Railway/Postgres (Npgsql).
// - Desactiva FK con session_replication_role=replica (orden de tablas irrelevante).
// - Idempotente: limpia cada tabla destino antes de cargar.

using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

// Npgsql 6+ exige Kind=Utc para timestamptz; el SqlDataReader entrega DateTime con Kind=Unspecified.
// El switch legacy mapea DateTime <-> 'timestamp without time zone' sin exigir Kind (igual que la app).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: dotnet run -- \"<sqlserver-conn>\" \"<postgres-conn>\"");
    return 1;
}

var srcConn = args[0];
var dstConn = args[1];

using var src = new SqlConnection(srcConn);
src.Open();
using var dst = new NpgsqlConnection(dstConn);
dst.Open();

// Desactiva FK/triggers en la sesión destino para no depender del orden de inserción.
using (var cmd = new NpgsqlCommand("SET session_replication_role = replica;", dst))
    cmd.ExecuteNonQuery();

// Tablas base del origen (excluye el historial de migraciones de EF).
var tables = new List<string>();
using (var cmd = new SqlCommand(
    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
    "WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory' ORDER BY TABLE_NAME", src))
using (var r = cmd.ExecuteReader())
    while (r.Read())
        tables.Add(r.GetString(0));

// Primero limpia todas las tablas destino (idempotencia; FK off => orden irrelevante).
foreach (var table in tables)
    using (var del = new NpgsqlCommand($"DELETE FROM \"{table}\";", dst))
        del.ExecuteNonQuery();

long grandTotal = 0;
foreach (var table in tables)
{
    using var selectCmd = new SqlCommand($"SELECT * FROM [{table}]", src);
    using var reader = selectCmd.ExecuteReader();

    var fieldCount = reader.FieldCount;
    var colNames = new string[fieldCount];
    var npgTypes = new NpgsqlDbType[fieldCount];
    for (var i = 0; i < fieldCount; i++)
    {
        colNames[i] = reader.GetName(i);
        npgTypes[i] = MapType(reader.GetFieldType(i));
    }

    var colList = string.Join(",", colNames.Select(c => "\"" + c + "\""));
    var paramList = string.Join(",", Enumerable.Range(0, fieldCount).Select(i => "@p" + i));
    var insertSql = $"INSERT INTO \"{table}\" ({colList}) VALUES ({paramList})";

    var count = 0;
    using var tx = dst.BeginTransaction();
    while (reader.Read())
    {
        using var ins = new NpgsqlCommand(insertSql, dst, tx);
        for (var i = 0; i < fieldCount; i++)
        {
            var val = reader.GetValue(i);
            var p = new NpgsqlParameter("p" + i, npgTypes[i]) { Value = val ?? DBNull.Value };
            ins.Parameters.Add(p);
        }
        ins.ExecuteNonQuery();
        count++;
    }
    tx.Commit();

    grandTotal += count;
    Console.WriteLine($"{table,-40} {count,8}");
}

Console.WriteLine(new string('-', 50));
Console.WriteLine($"{"TOTAL filas",-40} {grandTotal,8}");
Console.WriteLine($"{tables.Count} tablas migradas.");
return 0;

static NpgsqlDbType MapType(Type t) => t switch
{
    _ when t == typeof(Guid) => NpgsqlDbType.Uuid,
    _ when t == typeof(bool) => NpgsqlDbType.Boolean,
    _ when t == typeof(byte) => NpgsqlDbType.Smallint,
    _ when t == typeof(short) => NpgsqlDbType.Smallint,
    _ when t == typeof(int) => NpgsqlDbType.Integer,
    _ when t == typeof(long) => NpgsqlDbType.Bigint,
    _ when t == typeof(decimal) => NpgsqlDbType.Numeric,
    _ when t == typeof(double) => NpgsqlDbType.Double,
    _ when t == typeof(float) => NpgsqlDbType.Real,
    _ when t == typeof(DateTime) => NpgsqlDbType.Timestamp,
    _ when t == typeof(DateTimeOffset) => NpgsqlDbType.TimestampTz,
    _ when t == typeof(TimeSpan) => NpgsqlDbType.Interval,
    _ when t == typeof(byte[]) => NpgsqlDbType.Bytea,
    _ => NpgsqlDbType.Text
};
