# Control de cuadre sobre una copia de producción (opción A)

Instructivo para correr en la máquina local (Windows, `C:\Eiti\eiti`), antes del deploy de la rama
`claude/cuenta-corriente-proveedores-jkakup`. Lo puede seguir una persona o una sesión de Claude Code local.

## Qué se busca

La rama trae el extracto de cuenta corriente con saldo acumulado. Antes de que lo vean los clientes, hay que
saber si en alguna cuenta el **saldo final del extracto** no coincide con lo que muestran las **tarjetas**
(saldo pendiente − saldo a favor). La causa conocida es un bug viejo (arreglado en la rama, commit `6ba923b`):
anular un pago o cobro que había usado saldo a favor previo **borraba ese saldo**. Esas cuentas aparecen con
diferencia negativa.

Tres herramientas, todas en `tools/LedgerCheck/`:

| Archivo | Qué hace | ¿Escribe? |
|---|---|---|
| `Program.cs` (`dotnet run --project tools/LedgerCheck`) | Corre los mismos handlers que la API sobre cada cuenta y lista las que no cuadran. Necesita la base con la migración de la rama. | No |
| `saldo-a-favor-perdido.sql` | Calcula, por cuenta, el saldo a favor que se perdió por el bug. Sirve sin la migración. | No |
| `devolver-saldo-a-favor.sql` | Plantilla que devuelve ese saldo, con controles: si el saldo actual no es el diagnosticado, aborta sin tocar nada. | **Sí** |

## Reglas

- **Producción solo se lee**, y una sola vez: el `pg_dump` del paso 2. Todo lo demás corre sobre la copia local.
- La connection string de producción va en una variable de la consola (`$env:PROD_URL`). **Nunca** en un
  archivo, en un commit ni pegada en un chat.
- El dump tiene datos de clientes: se guarda en `%TEMP%` y **se borra al terminar** (paso 9).
- `devolver-saldo-a-favor.sql` **no se corre en producción** sin el OK explícito del dueño. Se ensaya en la copia.

## Requisitos

- Rama al día: `git fetch origin; git checkout claude/cuenta-corriente-proveedores-jkakup; git pull`.
- PostgreSQL local corriendo, con `psql`, `pg_dump`, `pg_restore`, `createdb` y `dropdb` en el PATH.
  `pg_dump` tiene que ser de la **misma versión mayor o más nueva** que el Postgres de Railway.
- .NET 10 SDK y `dotnet-ef` 8.x (`dotnet tool install --global dotnet-ef --version 8.0.11` si no está).

## Pasos (PowerShell, desde `C:\Eiti\eiti`)

### 0. Variables de la consola

```powershell
# Railway → servicio Postgres → Variables → DATABASE_PUBLIC_URL (postgresql://...). Solo en esta consola.
$env:PROD_URL   = "<DATABASE_PUBLIC_URL>"
$env:PGPASSWORD = "<clave del Postgres LOCAL>"   # la usan psql/createdb/pg_restore contra localhost
$COPIA = "Host=localhost;Port=5432;Database=eiti_copia_prod;Username=postgres;Password=$($env:PGPASSWORD)"
```

### 1. Versiones

```powershell
psql $env:PROD_URL -tAc "show server_version"
pg_dump --version
```

Si `pg_dump` es más viejo que el servidor, actualizar las herramientas cliente antes de seguir.

### 2. Copia de producción (única lectura de producción)

```powershell
pg_dump $env:PROD_URL -Fc --no-owner -f "$env:TEMP\eiti-prod.dump"
```

### 3. Restaurar la copia en local

```powershell
dropdb -U postgres --if-exists eiti_copia_prod
createdb -U postgres eiti_copia_prod
pg_restore -U postgres -d eiti_copia_prod --no-owner --no-privileges "$env:TEMP\eiti-prod.dump"
# Por stdin: PowerShell 5.1 se come las comillas dobles de un argumento -c.
'select "MigrationId" from "__EFMigrationsHistory" order by 1 desc limit 3' | psql -U postgres -d eiti_copia_prod
```

Avisos de `pg_restore` sobre roles, dueños o permisos son esperables. Cualquier otro error se reporta.
La última migración debería ser `20261001185948_AddBranchFiscalPointOfSale` (lo que está en `main`). Si es
otra, producción no está al día con `main`: anotarlo en el reporte.

### 4. Diagnóstico del bug (sobre la copia, antes de migrar)

```powershell
psql -U postgres -d eiti_copia_prod -f tools\LedgerCheck\saldo-a-favor-perdido.sql | Tee-Object "$env:TEMP\saldo-perdido.txt"
```

Lista, por cuenta, `saldo_perdido` y `saldo_a_favor_actual`. Si sale vacío, ninguna anulación perdió saldo.

### 5. Aplicar la migración de la rama a la copia

```powershell
dotnet ef database update --project eiti.Infrastructure --startup-project eiti.Infrastructure --connection $COPIA
```

Tiene que decir `Applying migration '20261023021239_AddPaymentOrdersAndReceipts'` (y las de `main` que falten,
si el paso 3 mostró que producción estaba atrasada). No levanta la API: no hay riesgo de que salga nada
hacia ARCA ni hacia los mails.

### 6. Control de cuadre

```powershell
dotnet run --project tools/LedgerCheck -- $COPIA | Tee-Object "$env:TEMP\ledgercheck.txt"
```

Los importes salen en formato inglés (`50,000.00`). Última línea: `N cuentas revisadas · X con diferencia · Y con error`.

### 7. Cruzar los dos resultados

Para cada línea `DIFIERE` de `ledgercheck.txt`, buscar el mismo id en `saldo-perdido.txt`:

| Caso | Qué significa | Qué se hace |
|---|---|---|
| Diferencia **negativa** y `saldo_perdido` igual a esa diferencia en valor absoluto | Saldo a favor perdido por el bug. **Confirmado.** | Entra en la corrección (paso 8). |
| Diferencia negativa sin `saldo_perdido`, o con otro número | Otra causa, o el bug más algo más. | No se corrige: se reporta para investigar. |
| Diferencia **positiva** | Otra causa. | Se reporta para investigar. |
| Línea `ERROR` | La consulta de esa cuenta falló. | Se reporta con el mensaje. |

### 8. Ensayar la corrección en la copia

1. Copiar la plantilla fuera del repo: `Copy-Item tools\LedgerCheck\devolver-saldo-a-favor.sql "$env:TEMP\devolver.sql"`.
2. En la copia de `%TEMP%`, agregar una línea por cuenta **confirmada**, con el saldo actual y el perdido
   del paso 4: `SELECT pg_temp.devolver_saldo('proveedor', '<id>', <saldo_a_favor_actual>, <saldo_perdido>);`
   (o `'cliente'`).
3. Correrla en la copia: `psql -U postgres -d eiti_copia_prod -v ON_ERROR_STOP=1 -f "$env:TEMP\devolver.sql"`.
4. Repetir el paso 6: las cuentas corregidas tienen que desaparecer del listado.

Ese `devolver.sql` completo es el que, con el OK del dueño, se aplica en producción **después del deploy**:
con el código viejo corriendo, una anulación nueva podría volver a perder saldo entre el diagnóstico y la
corrección. Después de aplicarlo, correr el paso 6 contra producción (solo lee) para confirmar.

### 9. Limpieza

```powershell
Remove-Item "$env:TEMP\eiti-prod.dump"
dropdb -U postgres eiti_copia_prod
Remove-Item Env:PROD_URL, Env:PGPASSWORD
```

`devolver.sql`, `saldo-perdido.txt` y `ledgercheck.txt` se guardan hasta aplicar la corrección y después se
borran. Ninguno se commitea.

## Qué reportar

- Versión del servidor, última migración de producción (paso 3) y avisos de `pg_restore` que no sean de roles,
  dueños o permisos.
- La última línea de `ledgercheck.txt`.
- Por cada cuenta con diferencia: empresa, proveedor/cliente, nombre, diferencia, `saldo_perdido`, y en qué
  caso de la tabla del paso 7 cae.
- Si se ensayó la corrección: el resultado del paso 6 después de aplicarla en la copia.
- **Sin** connection strings ni contraseñas.

## Ensayo hecho en la sesión remota (2026-10-23)

Base nueva migrada con la API de `main`; con esa misma API se reprodujo el bug (factura de 100.000 + 50.000
de saldo a favor, pago de 30.000, anulación) en un proveedor y en un cliente: los dos quedaron con saldo a
favor 0. Resultados:

- `saldo-a-favor-perdido.sql`: 50.000 perdidos en cada cuenta.
- `dotnet ef database update --connection`: aplicó solo `AddPaymentOrdersAndReceipts`.
- `LedgerCheck`: las dos cuentas con diferencia −50.000.
- `devolver-saldo-a-favor.sql` con un saldo actual equivocado: abortó sin tocar ninguna de las dos cuentas.
  Con los valores diagnosticados: `LedgerCheck` volvió a 0 diferencias.
