using Npgsql;

var conn = args[0];
using var db = new NpgsqlConnection(conn);
db.Open();

var tables = new List<string>();
using (var cmd = new NpgsqlCommand(
    "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename", db))
using (var r = cmd.ExecuteReader())
    while (r.Read()) tables.Add(r.GetString(0));

Console.WriteLine($"{"Tabla",-40} {"Filas",10}");
Console.WriteLine(new string('-', 52));
long total = 0;
foreach (var t in tables)
{
    using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{t}\"", db);
    var n = Convert.ToInt64(cmd.ExecuteScalar());
    total += n;
    Console.WriteLine($"{t,-40} {n,10}");
}
Console.WriteLine(new string('-', 52));
Console.WriteLine($"{"TOTAL",-40} {total,10}");
