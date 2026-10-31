# Descuento minorista con tope y reporte — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cualquier vendedor aplica un descuento % a la venta minorista dentro de un máximo que fija el dueño; cada descuento queda trazado (quién, cuándo, motivo) y hay un reporte para auditarlos.

**Architecture:** Usa `Sale.GeneralDiscountPercent` (ya existe y ya imprime "Bonificación general" en la factura). Se agregan a la venta los datos de trazabilidad, a la empresa el tope `MaxRetailDiscountPercent` (comando y permiso propios, no `UpdateCurrentCompany`), la validación del tope en `CreateSale`/`UpdateSale` (solo ventas no CC), un reporte `Descuentos` y el prorrateo del descuento general en el reporte de rentabilidad. Back y front trabajan en paralelo contra el contrato de abajo.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core + Npgsql, xUnit · Angular 16 standalone, Jasmine/Karma, XLSX.

**Spec:** `docs/superpowers/specs/2026-10-29-notas-recargo-descuento-design.md`, sección **3**.

## Global Constraints

- Back: worktree `C:/Eiti/eiti/.claude/worktrees/feature+notas-recargo-descuento`; front: `C:/EiTeFront/eiti-front/.claude/worktrees/notas-recargo-descuento`; rama `feature/notas-recargo-descuento` en los dos. Commits chicos con el cierre de atribución habitual; push a esa rama; nunca a `main`/`develop`.
- **Tope:** `Company.MaxRetailDiscountPercent`, decimal 0–100, **default 0 = descuento deshabilitado**. Rige solo para ventas **no CC**. Las CC siguen sin límite.
- **Qué valida el tope** (back, venta no CC): `GeneralDiscountPercent` y cada `SaleDetail.DiscountPercent` ≤ tope. En `CreateSale` siempre; en `UpdateSale` solo lo que **cambió** respecto de la venta guardada (el % general si cambió; un renglón si su descuento cambió). Error: `Error.Validation("Sales.Discount.ExceedsLimit", "El descuento supera el máximo de {X}% que fijó el dueño.")` en un `*Errors.cs`.
- **Trazabilidad en `Sale`:** `GeneralDiscountByUserId : Guid?`, `GeneralDiscountAt : DateTime?` (UTC), `GeneralDiscountReason : string?` (≤ 200, trim, vacío = null). Se fijan **solo cuando el % general cambia** (alta con % > 0 cuenta como cambio). Si el % vuelve a 0, los tres quedan en null. Método de dominio, nada de setters públicos.
- **Permisos nuevos** (los dos en su bloque temático, nunca al final; `PermissionCodes.cs`, `RoleCatalog.cs`, **`PermissionCatalog.All`**, y en el front `PermissionCodes` + `PermissionCatalog` de `permission.models.ts`):
  - `sales.discount.configure` → `SalesDiscountConfigure` / `salesDiscountConfigure`, etiqueta "Ventas: fijar el descuento máximo", solo **Owner**.
  - `reports.sales.discounts` → `ReportsSalesDiscounts` / `reportsSalesDiscounts`, etiqueta "Reportería: descuentos", a Owner y a los mismos roles que hoy tienen `reports.sales.trade_ins`.
  - Sin migración de datos de permisos (se asignan a mano desde Perfiles de acceso).
- **Fechas del reporte** con `BusinessCalendar.ToUtcRange` (nunca `DateFrom.Date` contra UTC). Excluye ventas anuladas. Solo ventas con `GeneralDiscountPercent > 0`.
- Migración EF única (`AddRetailDiscountTrace`), desde `eiti.Infrastructure`. La API corriendo bloquea DLLs: no levantarla mientras se compila.
- UI: invocar `/frontend-design` antes de pantallas nuevas; controles de la app (`app-searchable-select`, `toggle-check`), nada nativo nuevo; textos en español rioplatense.
- Verificación: back `dotnet build eiti.Application/eiti.Application.csproj` + `dotnet test eiti.Tests`; front `ng build --configuration development` + `ng test --watch=false --browsers=ChromeHeadless`.

## Contrato de la API (lo que el front consume)

| Qué | Contrato |
|---|---|
| Empresa | `GET /api/companies/current` suma `maxRetailDiscountPercent: number` |
| Fijar el tope | `PUT /api/companies/current/retail-discount-limit` body `{ "maxPercent": number }` (0–100) → 204; permiso `sales.discount.configure`; fuera de rango → 400 |
| Alta / edición | `POST /api/sales` y `PUT /api/sales/{id}` aceptan `generalDiscountPercent` (ya existe) y **`generalDiscountReason?: string \| null`** |
| Tope excedido | 400, `errorCode: "Sales.Discount.ExceedsLimit"`, `detail` con el mensaje |
| Lecturas | `ListSalesItemResponse` y `GetSaleByIdResponse` traen `generalDiscountPercent` y `generalDiscountReason` |
| Reporte | `GET /api/reports/sales/discounts?dateFrom=YYYY-MM-DD&dateTo=YYYY-MM-DD&branchId=&userId=` → `{ rows: [{ saleId, saleCode, date, branchName, customerName, isCuentaCorriente, appliedByUserId, appliedByUserName, discountPercent, subtotalBeforeDiscount, discountAmount, total, reason }], totals: { salesCount, discountAmount } }`; permiso `reports.sales.discounts`. `subtotalBeforeDiscount` = `OriginalTotal`; `discountAmount` = `OriginalTotal − total sin override` redondeado a 2; `customerName` "Consumidor final" si no hay cliente; filas ordenadas por fecha descendente. |

## Review Focus

- Vendedor manda 15% con tope 10% → 400 con el mensaje, la venta no se crea. → test back.
- Tope 0 (sin configurar) y el front no muestra el control; por API un % > 0 en minorista → 400. → test back.
- Editar una venta en espera que ya tenía 15% después de bajar el tope a 10%, sin tocar el descuento → se guarda. → test back.
- "Cobrar" rápido de una venta con descuento → el descuento y el motivo se conservan. → test front.
- Venta minorista con descuento y pago con tarjeta: el total que se cobra ya trae el descuento (sin "saldo pendiente"). → test front + e2e.

---

### Task B1 (back): dominio, tope de empresa y permisos
Sale (3 campos + método), `Company.MaxRetailDiscountPercent` + `SetMaxRetailDiscountPercent(decimal)` (lanza fuera de 0–100), EF + migración, `SetRetailDiscountLimit` (comando/handler/validator/errores, en `Features/Companies/Commands/SetRetailDiscountLimit/`), endpoint, `GetCurrentCompany` con el campo nuevo, los dos permisos en las tres listas. Tests: dominio (trazabilidad al cambiar/limpiar), handler del tope (fuera de rango, ok).

### Task B2 (back): validación del tope y trazabilidad en alta/edición
`CreateSale`/`UpdateSale`: `GeneralDiscountReason` en el comando (+ validator ≤ 200), validación del tope según Global Constraints, llamada al método de trazabilidad con `ICurrentUserService.UserId` y `DateTime.UtcNow`. Lecturas con `GeneralDiscountPercent`/`GeneralDiscountReason`. Tests: los tres primeros puntos del Review Focus + CC sin límite + motivo > 200.

### Task B3 (back): reporte de descuentos y prorrateo en rentabilidad
`Features/Reports/Queries/DiscountsReport/` (query/handler/response/validator) + endpoint; `SalesReportHandler`: ingreso del renglón × (1 − `GeneralDiscountPercent`/100). Tests: reporte (filtros, excluye anuladas, totales, nombre del usuario) y prorrateo (venta con 10% → ingreso y ganancia bajan 10%).

### Task F1 (front): permisos, modelos, servicios y tope en Empresa
Permisos en `permission.models.ts` (bloques temáticos), modelos (`maxRetailDiscountPercent`, `generalDiscountReason`, tipos del reporte en `core/models/report.models.ts`), métodos de servicio (`setRetailDiscountLimit`, `getDiscountsReport`), campo "Descuento máximo en ventas minoristas (%)" en la pantalla Empresa con su botón Guardar, visible solo con `sales.discount.configure`.

### Task F2 (front): descuento en la venta minorista
En alta y edición de `sales-page`: botón "Descuento" que despliega % (máx. = tope, con texto "hasta X%") y "Motivo (opcional)"; no se muestra si el tope es 0. El total que ve el vendedor, el que recibe `app-sale-payment-inline` y la base del recargo ya aplican el descuento (misma cuenta que `Sale.RecalculateTotal`: (renglones + recargo sin envío) × (1 − %/100)). `buildRequest` manda `generalDiscountPercent` y `generalDiscountReason`. El "Cobrar" rápido de Gestión de Ventas reenvía los dos tal como están. La tarjeta de la venta muestra "Descuento X% · motivo". Tests: request, total con descuento, cobro rápido conserva el descuento, control oculto con tope 0.

### Task F3 (front): pantalla del reporte de descuentos
`features/reports/discounts/` con el mismo esqueleto que `trade-ins-by-branch` (filtros fecha desde/hasta con "hoy" por `toLocaleDateString('en-CA')`, sucursal, usuario; tabla; totales; export Excel), ruta lazy `reports/discounts` con `data: { permission: PermissionCodes.reportsSalesDiscounts }`, entrada en el menú junto a los otros reportes de ventas. Tests del componente (arma la query, muestra totales).

### Task E (e2e local, la hace el orquestador)
Tope 0 → sin control; dueño fija 10% → aparece "hasta 10%"; venta con 15% → error; venta con 10% + motivo + factura → PDF con "Bonificación general 10%"; cobro rápido conserva el descuento; reporte lista la venta con usuario y motivo; rentabilidad baja 10%.
