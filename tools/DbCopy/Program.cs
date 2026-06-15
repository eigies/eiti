// Copia datos SQL Server -> PostgreSQL usando COPY (streaming binario), tabla por tabla.
// Uso:  DbCopy.exe "<sqlserver-conn>" "<postgres-conn>"
// - Fuente: solo lectura (SELECT *). NUNCA modifica el origen.
// - Destino: DELETE de cada tabla + COPY FROM STDIN (rapido). Idempotente.
// - Desactiva FK con session_replication_role=replica (orden de tablas irrelevante).
// - Lee los tipos reales de columna del destino para mapear correctamente (date/numeric/uuid/etc.).

using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: DbCopy.exe \"<sqlserver-conn>\" \"<postgres-conn>\"");
    return 1;
}

var srcConn = args[0];
var dstConn = args[1];

using var src = new SqlConnection(srcConn);
src.Open();
using var dst = new NpgsqlConnection(dstConn);
dst.Open();

// FK/triggers off en la sesion destino (orden de insercion irrelevante).
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

// Limpia todas las tablas destino primero (idempotencia; FK off => orden irrelevante).
foreach (var table in tables)
    using (var del = new NpgsqlCommand($"DELETE FROM \"{table}\";", dst))
        del.ExecuteNonQuery();

long grandTotal = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();

foreach (var table in tables)
{
    // Tipos reales de las columnas del destino.
    var targetTypes = new Dictionary<string, NpgsqlDbType>(StringComparer.OrdinalIgnoreCase);
    using (var cmd = new NpgsqlCommand(
        "SELECT column_name, data_type FROM information_schema.columns " +
        "WHERE table_schema='public' AND table_name=@t", dst))
    {
        cmd.Parameters.AddWithValue("t", table);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            targetTypes[r.GetString(0)] = MapPgType(r.GetString(1));
    }

    using var selectCmd = new SqlCommand($"SELECT * FROM [{table}]", src);
    using var reader = selectCmd.ExecuteReader();

    // Columnas presentes en origen Y destino.
    var cols = new List<string>();
    for (var i = 0; i < reader.FieldCount; i++)
    {
        var name = reader.GetName(i);
        if (targetTypes.ContainsKey(name))
            cols.Add(name);
    }

    var colList = string.Join(",", cols.Select(c => "\"" + c + "\""));
    var count = 0;

    using (var importer = dst.BeginBinaryImport(
        $"COPY \"{table}\" ({colList}) FROM STDIN (FORMAT BINARY)"))
    {
        while (reader.Read())
        {
            importer.StartRow();
            foreach (var col in cols)
            {
                var val = reader.GetValue(reader.GetOrdinal(col));
                if (val is null || val is DBNull)
                    importer.WriteNull();
                else
                    importer.Write(val, targetTypes[col]);
            }
            count++;
        }
        importer.Complete();
    }

    grandTotal += count;
    Console.WriteLine($"{table,-40} {count,10}");
}

sw.Stop();
Console.WriteLine(new string('-', 52));
Console.WriteLine($"{"TOTAL filas",-40} {grandTotal,10}");
Console.WriteLine($"{tables.Count} tablas migradas en {sw.Elapsed.TotalSeconds:F1}s.");
return 0;

static NpgsqlDbType MapPgType(string dataType) => dataType.ToLowerInvariant() switch
{
    "uuid" => NpgsqlDbType.Uuid,
    "boolean" => NpgsqlDbType.Boolean,
    "smallint" => NpgsqlDbType.Smallint,
    "integer" => NpgsqlDbType.Integer,
    "bigint" => NpgsqlDbType.Bigint,
    "numeric" => NpgsqlDbType.Numeric,
    "real" => NpgsqlDbType.Real,
    "double precision" => NpgsqlDbType.Double,
    "date" => NpgsqlDbType.Date,
    "timestamp without time zone" => NpgsqlDbType.Timestamp,
    "timestamp with time zone" => NpgsqlDbType.TimestampTz,
    "time without time zone" => NpgsqlDbType.Time,
    "interval" => NpgsqlDbType.Interval,
    "bytea" => NpgsqlDbType.Bytea,
    "character varying" => NpgsqlDbType.Varchar,
    "character" => NpgsqlDbType.Char,
    _ => NpgsqlDbType.Text
};
