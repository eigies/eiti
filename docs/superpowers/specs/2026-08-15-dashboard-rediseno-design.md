# Dashboard inicial — rediseño y endpoint agregado — Design Spec

## Contexto

El dashboard (`/dashboard`) es la primera pantalla que ve todo usuario después de loguearse. Hoy **no tiene backend**: se verificó por búsqueda en todo el repo que no existe ninguna feature ni controller de Dashboard. Lo único que existe es el permiso `dashboard.view_financials`.

Todo se calcula en el navegador a partir de **una sola llamada** en `ngOnInit`:

```ts
this.saleService.listSales({ dateFrom: <1ro del mes local>, includeCuentaCorriente: true })
```

De ahí salen ~35 getters derivados en `dashboard.component.ts` (461 líneas).

**Medición del volumen real** (Baterías Soler, la empresa más cargada):

| Período | Ventas | Líneas | Pagos | Payload estimado |
|---|---|---|---|---|
| 2026-07 (peor mes) | 834 | 908 | 787 | ~1,08 MB |
| 2026-08 (actual) | 388 | 411 | 371 | ~0,50 MB |

Conclusión de la medición: **hoy no está lento**. 834 objetos en memoria son triviales para un browser y los getters sin memoizar no se notan. El problema no es el presente sino el techo: el payload crece lineal con el volumen del cliente, y esta es la página que menos conviene que se degrade cuando entre un cliente más grande.

Además se detectó que **la API no tiene compresión de respuestas configurada** (no hay `AddResponseCompression` en `Program.cs`), así que ese ~1 MB viaja entero.

## Decisiones ya tomadas (confirmadas con el usuario)

- **Enfoque B: endpoint agregado en el backend.** Se descartó extender el cálculo en el front. Razón: el dashboard es la primera pantalla post-login y debe escalar sin rehacerse.
- **Un solo filtro de sucursal global** que manda sobre toda la pantalla (lectura del mes, gráfico y listado). Se descartó un filtro por widget para evitar que el usuario compare números de sucursales distintas sin darse cuenta.
- **El bloque principal muestra columnas MES y HOY**, cada una cortada en Total / Minorista / Cuenta Corriente. Esto absorbe el widget "Resumen de hoy", que desaparece como tarjeta independiente.
- **El cuadro muestra cantidad siempre y plata solo con `dashboard.view_financials`.**
- **`Amount` significa facturación de ventas activas** (no canceladas), no lo cobrado. El bloque de arriba responde "cuánto vendí"; el bloque secundario de Cobranza responde "cuánto cobré".
- **El top de productos se mantiene**, calculado en el server.
- Minorista = `!IsCuentaCorriente`; Cuenta Corriente = `IsCuentaCorriente`. Misma definición que `SalesReport` y `WholesaleByCustomer`.

## Alcance

Entra:

1. Endpoint `GET /api/dashboard/summary` con los agregados de la pantalla.
2. Rediseño del dashboard: nueva jerarquía de widgets y el cuadro MES/HOY por tipo.
3. Gráfico de ritmo comercial con series separadas minorista / CC.
4. Selector global de sucursal con persistencia de la última elección.
5. Compresión de respuestas en la API (transversal, beneficia toda la app).

No entra:

- Selector de rango de fechas / comparación entre meses. El período sigue siendo el mes en curso.
- Filtro de sucursal en `ListSalesQuery` (el drill-down filtra en cliente sobre un solo día).
- Rediseño visual del resto de la app.

## Backend

### Feature (Vertical Slice)

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/`

| Archivo | Contenido |
|---|---|
| `GetDashboardSummaryQuery.cs` | `record GetDashboardSummaryQuery(DateTime DateFrom, DateTime DateTo, Guid? BranchId) : IRequest<Result<GetDashboardSummaryResponse>>` |
| `GetDashboardSummaryHandler.cs` | El handler |
| `GetDashboardSummaryResponse.cs` | Los records de respuesta |
| `GetDashboardSummaryValidator.cs` | Fechas obligatorias, `DateFrom <= DateTo` |
| `GetDashboardSummaryErrors.cs` | `BranchNotAllowed` |

**Sin `IRequirePermissions`**: alcanza con estar autenticado, igual que hoy (la ruta `/dashboard` tiene `canActivate: [authGuard]` y ningún `permissionGuard`). Se evaluó exigir `SalesAccess` y se descartó: existe un perfil **"Cajero" sin ese permiso** en varias empresas — hoy con 0 usuarios activos, pero es un rol legítimo, y exigirlo le cerraría el dashboard a cualquier cajero futuro. Lo sensible es la plata, y eso se gatea con `DashboardViewFinancials`.

Por eso la query implementa `IRequest<...>` a secas, sin `IRequirePermissions`.

### Contrato de respuesta

```csharp
public sealed record GetDashboardSummaryResponse(
    DashboardPeriodTotals Month,
    DashboardPeriodTotals Today,
    IReadOnlyList<DashboardDayPoint> Days,          // últimos 7 días locales
    IReadOnlyList<DashboardTopProduct> TopProducts, // top 5 del mes
    DashboardCollections Collections,               // bloque Cobranza
    DashboardTodayStatus TodayStatus,               // bloque Pulso del día
    IReadOnlyList<DashboardRecentSale> RecentSales);// últimas 6

public sealed record DashboardPeriodTotals(
    DashboardSegment Total,
    DashboardSegment Retail,
    DashboardSegment CurrentAccount);

public sealed record DashboardSegment(int Count, decimal Amount);

public sealed record DashboardDayPoint(
    DateTime Date,
    int RetailCount, decimal RetailAmount,
    int CurrentAccountCount, decimal CurrentAccountAmount);

public sealed record DashboardTopProduct(
    Guid ProductId, string Name, string Brand, int Units, int SalesCount);

public sealed record DashboardCollections(
    decimal PaidAmount, int PaidCount,
    decimal PendingAmount, int PendingCount,
    decimal AvgTicket);

public sealed record DashboardTodayStatus(
    int ActiveCount, int PaidCount, int PendingCount, int CancelledCount);

public sealed record DashboardRecentSale(
    Guid Id, string? Code, DateTime CreatedAt,
    string CustomerName, int SaleStatus, decimal TotalAmount, bool IsCuentaCorriente);
```

Peso estimado: **~6 KB**, constante, independiente del volumen del cliente.

### Reglas del handler

1. `EnsureAuthenticated()` como primera línea, según el patrón del repo.
2. **Fechas por `BusinessCalendar`**: `ToUtcRange(request.DateFrom, request.DateTo)` para el mes, y `StartOfDayUtc`/`EndOfDayUtc` sobre el día local de hoy para la columna HOY y para los 7 días del gráfico. El "hoy" lo calcula el server, no llega por parámetro.
3. **Sucursal**: si `BranchId` viene y el usuario no tiene `branches.view_all` ni esa sucursal en `AllowedBranchIds`, devuelve `Result.Failure(BranchNotAllowed)`. Si no viene, se acotan las ventas a `AllowedBranchIds` cuando `!CanViewAllBranches`.
4. **Plata gateada en el server**: si el usuario no tiene `DashboardViewFinancials`, todos los `Amount`, `PaidAmount`, `PendingAmount` y `AvgTicket` salen en `0`. Hoy `listSales` manda todos los montos y el front los esconde con `*ngIf`, o sea que están en el network tab de cualquiera. Este cambio lo corrige.
5. **Canceladas excluidas** de `Count` y `Amount` en todos los segmentos; se reportan aparte en `TodayStatus.CancelledCount`.

### Repositorio

`ISaleRepository.ListForSalesReportAsync` ya devuelve ventas del período con `Include(Details)`, filtrando canceladas y respetando sucursales permitidas. **Se reutiliza tal cual** — no se agrega método nuevo.

**`Collections` se calcula por `SaleStatus`, no por importes de pago.** Ojo con esto: `Sale.MonetaryPaidAmount` es `_payments.Sum(...)` y `Sale.PendingAmount` se deriva de ahí, así que **sin `Include(Payments)` devuelven 0**. No hace falta cargar los pagos: el dashboard actual ya calcula cobrado y pendiente por estado, y se replica ese criterio.

```
PaidAmount    = suma de TotalAmount de las ventas con SaleStatus = Paid
PendingAmount = suma de TotalAmount de las ventas con SaleStatus = OnHold
AvgTicket     = suma de TotalAmount de las activas / cantidad de activas
```

Mismo resultado que hoy, sin traer una tabla más.

### Controller

`eiti.Api/Controllers/DashboardController.cs`, siguiendo el patrón de `ReportsController`: `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]`, un `ISender`, y la acción `[HttpGet("summary")]` con `[FromQuery]`.

### Compresión de respuestas

En `Program.cs`: `AddResponseCompression` con Brotli + Gzip habilitado para `application/json`, y `UseResponseCompression()` en el pipeline antes de los endpoints. Baja el JSON de la app entera ~85%.

## Frontend

### Archivos

| Archivo | Estado |
|---|---|
| `core/models/dashboard.models.ts` | nuevo — interfaces espejo del contrato |
| `core/services/dashboard.service.ts` | nuevo — `getSummary(dateFrom, dateTo, branchId?)` |
| `core/services/dashboard-preferences.service.ts` | nuevo — persistencia de filtros |
| `features/dashboard/dashboard.component.ts` | reescrito |
| `features/dashboard/dashboard.component.html` | reescrito |
| `features/dashboard/dashboard.component.css` | ajustado a la nueva jerarquía |

El componente adelgaza fuerte: los ~35 getters derivados desaparecen porque el server ya devuelve todo calculado. Queda estado de UI, los toggles y el formateo.

**Antes de escribir el front hay que invocar la skill `/frontend-design`**, según el `CLAUDE.md` del proyecto.

### Jerarquía de la pantalla

```
HEADER                    Operación viva para <user>   [ Sucursal ▾ ]

PRIMER PLANO   ┌─ LECTURA DEL MES EN CURSO ─────────────────┐
               │              MES              HOY          │
               │  TOTAL       312   $8.4M      11   $312K    │
               │  Minorista   280   $6.1M      10   $280K    │
               │  Cta. Cte.    32   $2.3M       1    $32K    │
               └────────────────────────────────────────────┘

               ┌─ RITMO COMERCIAL · últimos 7 días ─────────┐
               │  [Ambas|Minorista|CC]  [Cantidad|Monto|Productos] │
               │  2 barras por día                          │
               └────────────────────────────────────────────┘

               ┌─ VENTAS (últimas 6 / filtradas) ───────────┐

SECUNDARIO     ┌ COBRANZA ┐ ┌ PULSO DEL DÍA ┐ ┌ ALERTAS ┐
```

Destino de cada widget actual:

| Hoy | Después |
|---|---|
| "Resumen de hoy" (hero) | absorbido como columna HOY |
| "Lectura del mes en curso" (KPI) | promovido a primer plano con el desglose |
| "Alertas operativas" (hero) | baja a secundario |
| "Ingresos cobrados" / "Pendiente de cobro" | bajan al bloque Cobranza |
| "Ticket promedio" | baja al bloque Cobranza |
| "Ritmo comercial" | se queda arriba, con dos series |
| "Pulso del día" | baja a secundario, sin cambios |
| "Últimas ventas" + drill-down | se queda, respetando la sucursal |

### Restricciones visuales

El rediseño es de **jerarquía y contenido, no de identidad visual**. Se reutilizan las clases y los tokens que el dashboard ya tiene (`hero-card`, `kpi-card`, `data-panel`, `panel-toggle`, `pulse-card`) y se reordenan.

- **Todos los colores salen de custom properties existentes** (`--amber`, `--danger`, `--success`, `--text`, `--bg-panel`, `--border-2`, …). Cero valores hardcodeados.
- Debe verse correcto en `theme-dark` y `theme-light`. Cualquier color nuevo se compone con `color-mix` sobre los tokens, como ya hace el resto del CSS.
- Si aparece una oportunidad clara de mejorar la lectura con un gráfico o un elemento nuevo, se puede sumar — pero atado a la paleta y verificado en los dos temas. Ante la duda, se mantiene lo que ya existe.
- **Las dos series del gráfico necesitan distinguirse.** La paleta define `--amber`, `--success` y `--danger` en los dos temas. Se usa **`--amber` para minorista** (es el acento principal y el volumen dominante) y **`--success` para cuenta corriente**. Dos tonos del mismo ámbar no se distinguen bien en barras finas, y `--danger` está reservado para estados de error. La leyenda nombra cada serie, así el color no es el único portador de la información.

### Estado y persistencia

Estado del componente:

```ts
branchId: string | null           // null = todas
chartSegment: 'both' | 'retail' | 'cc'
chartMetric: 'count' | 'amount' | 'products'
selectedDayKey: string | null     // drill-down, NO persiste
selectedStatusKey: 'paid' | 'pending' | 'cancelled' | null   // NO persiste
```

`DashboardPreferencesService` persiste **solo los tres primeros**, con claves `eiti_dashboard_*`, dentro de `try/catch` y validando al restaurar — mismo patrón que `products.component.ts` usa para el layout del catálogo, pero encapsulado en un servicio de `core/` para respetar el espíritu de la regla del `CLAUDE.md` sobre `localStorage`.

**Validación al restaurar (importante):** si la sucursal guardada ya no existe o el usuario perdió acceso a ella, se descarta y se cae al default. Si el usuario no tiene `branches.view_all`, `null` ("Todas") no es una opción válida y se cae a su primera sucursal.

### Llamadas de red

| Acción | Requests |
|---|---|
| Entrar a la pantalla | 1 (`summary`) + 1 (`listBranches`, solo si hay más de una) |
| Cambiar sucursal | 1 (`summary`) |
| Cambiar Ambas/Minorista/CC | **0** — la respuesta ya trae las dos series |
| Cambiar Cantidad/Monto/Productos | **0** |
| Click en un día | 1 (`listSales` de ese día, acotado) |

El drill-down por día filtra por sucursal en cliente, sobre los datos de un solo día. No se agrega `BranchId` a `ListSalesQuery`.

### Manejo de errores

- Falla `summary` → `ToastService.error` + estado vacío con botón de reintentar. Nunca pantalla en blanco.
- Falla `listBranches` → cae a "Todas" y se oculta el selector.
- Falla el drill-down → toast, el panel queda como estaba.

## Testing

Backend (`eiti.Tests`):

- Segmentación minorista / CC sobre un set con ambos tipos.
- Canceladas excluidas de `Count` y `Amount`, y contadas en `TodayStatus`.
- Columna HOY vs MES usando `BusinessCalendar` — incluye el caso borde de una venta a las 21:10 local, que debe caer en el día correcto.
- Usuario sin `DashboardViewFinancials` recibe todos los importes en `0`.
- Usuario sin `branches.view_all` pidiendo una sucursal ajena recibe `BranchNotAllowed`.
- Serie de 7 días: longitud fija y días sin ventas en `0`, no ausentes.
- `Collections` calculado por estado: una venta `Paid` suma a `PaidAmount` y una `OnHold` a `PendingAmount`, sin cargar `Payments`.

Frontend:

- `DashboardPreferencesService`: guarda, restaura, descarta valor inválido, descarta sucursal sin acceso.
- Build de producción sin errores (`ng build --configuration development`).

## Riesgos / notas

- **El componente se reescribe, no se parchea.** Es el riesgo principal: hay que verificar widget por widget contra la tabla de destino para no perder nada en el camino.
- **Los importes en `0` para usuarios sin permiso** son indistinguibles de "no hubo ventas". El front no debe mostrar `$0` en esos casos: directamente no renderiza la columna de plata.
- El endpoint no cachea. Si más adelante pesa, el lugar natural es el `RuntimeCache`, pero hoy no hace falta y no se implementa (YAGNI).
- **No se crea ningún permiso nuevo**, así que no hay que tocar `PermissionCodes`, `PermissionCatalog`, `RoleCatalog` ni `permission.models.ts`, ni reiniciar la API por el allowlist estático.
- El endpoint devuelve datos de ventas a cualquier usuario autenticado, igual que hoy. Si en algún momento se quiere restringir quién ve el dashboard, es una decisión de producto aparte y hay que revisar antes qué perfiles quedarían afuera.
