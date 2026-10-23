// Control de cuadre del extracto de cuenta corriente, de solo lectura.
//
// Para cada proveedor y cada cliente con movimientos, corre los MISMOS handlers que usa la API
// (GetSupplierLedger / GetSupplierAccount y sus pares de clientes) y compara:
//     saldo final del extracto  ==  saldo pendiente − saldo a favor   (lo que muestran las tarjetas)
// Una diferencia significa que el extracto y la cuenta cuentan historias distintas sobre los mismos datos.
//
// Uso: dotnet run --project tools/LedgerCheck -- "<connection string>"
// La base tiene que tener las migraciones de esta rama aplicadas (usa el mismo modelo que la API).
// No escribe nada: solo manda queries (ninguna termina en "Command", así que tampoco audita).
using eiti.Application;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Customers.Queries.GetCustomerAccount;
using eiti.Application.Features.Customers.Queries.GetCustomerLedger;
using eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;
using eiti.Application.Features.Suppliers.Queries.GetSupplierLedger;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using eiti.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/LedgerCheck -- \"<connection string>\"");
    return 2;
}

var connectionString = args[0];
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
    .Build();

var toolUser = new ToolUser();
var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<IConfiguration>(configuration);
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddSingleton<ICurrentUserService>(toolUser);
await using var provider = services.BuildServiceProvider();

var accounts = new List<(string Kind, Guid CompanyId, Guid Id, string Name)>();
await using (var db = new NpgsqlConnection(connectionString))
{
    await db.OpenAsync();
    await using var cmd = new NpgsqlCommand("""
        SELECT 'proveedor', s."CompanyId", s."Id", s."Name" FROM "Suppliers" s
        UNION ALL
        SELECT 'cliente', c."CompanyId", c."Id", c."Name" FROM "Customers" c
        WHERE EXISTS (SELECT 1 FROM "Sales" v WHERE v."CustomerId" = c."Id" AND v."IsCuentaCorriente")
           OR EXISTS (SELECT 1 FROM "CustomerPayments" p WHERE p."CustomerId" = c."Id")
        ORDER BY 1, 2, 4
        """, db);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        accounts.Add((reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? "?" : reader.GetString(3)));
}

int checkedCount = 0, mismatches = 0, errors = 0;
foreach (var (kind, companyId, id, name) in accounts)
{
    toolUser.CompanyId = new CompanyId(companyId);
    await using var scope = provider.CreateAsyncScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

    decimal? closing, pending, credit;
    string? error;
    if (kind == "proveedor")
    {
        var ledger = await sender.Send(new GetSupplierLedgerQuery(id));
        var account = await sender.Send(new GetSupplierAccountQuery(id));
        closing = ledger.IsSuccess ? ledger.Value.ClosingBalance : null;
        pending = account.IsSuccess ? account.Value.SaldoPendiente : null;
        credit = account.IsSuccess ? account.Value.SaldoAFavor : null;
        error = ledger.IsFailure ? ledger.Error.Description : account.IsFailure ? account.Error.Description : null;
    }
    else
    {
        var ledger = await sender.Send(new GetCustomerLedgerQuery(id));
        var account = await sender.Send(new GetCustomerAccountQuery(id));
        closing = ledger.IsSuccess ? ledger.Value.ClosingBalance : null;
        pending = account.IsSuccess ? account.Value.SaldoPendiente : null;
        credit = account.IsSuccess ? account.Value.SaldoAFavor : null;
        error = ledger.IsFailure ? ledger.Error.Description : account.IsFailure ? account.Error.Description : null;
    }

    checkedCount++;
    if (error is not null)
    {
        errors++;
        Console.WriteLine($"ERROR  {kind,-9} {companyId} {name}: {error}");
        continue;
    }

    var expected = pending!.Value - credit!.Value;
    if (closing!.Value != expected)
    {
        mismatches++;
        Console.WriteLine($"DIFIERE {kind,-9} {companyId} {name} ({id}): extracto {closing:N2} · cuenta {expected:N2} (pendiente {pending:N2} − a favor {credit:N2}) · diferencia {closing - expected:N2}");
    }
}

Console.WriteLine($"\n{checkedCount} cuentas revisadas · {mismatches} con diferencia · {errors} con error");
return mismatches + errors == 0 ? 0 : 1;

// Usuario de la herramienta: ve la empresa que se está revisando y tiene todos los permisos.
sealed class ToolUser : ICurrentUserService
{
    public CompanyId? CompanyId { get; set; }
    public bool IsAuthenticated => true;
    public UserId? UserId { get; } = new(Guid.Empty);
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
    public bool HasPermission(string permission) => true;
    public IReadOnlyCollection<Guid> AllowedBranchIds => [];
    public bool CanViewAllBranches => true;
}
