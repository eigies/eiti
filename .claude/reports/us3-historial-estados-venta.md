# US3 — Historial de estados de venta

**Historia de usuario:**
Como dueño del negocio, quiero que las ventas canceladas permanezcan visibles en el historial con un estado específico, para mantener la integridad del registro de movimientos y auditar acciones realizadas por los usuarios.

**Criterios de aceptación:**
1. Al cancelar una venta, esta NO se borra de la grilla de ventas.
2. Los montos de ventas canceladas NO se suman a los totales de caja diarios.
3. El movimiento de cancelación SÍ se refleja en el historial de la caja (visible para auditoría).

---

## Análisis del estado actual

### Lo que ya existe
- `SaleStatus.Cancel (3)` ya existe en el dominio.
- `UpdateSaleHandler` ya permite OnHold → Cancel (sin movimiento de caja, correcto ya que nunca fue pagada).
- El frontend ya tiene chip visual "Cancelada" y clase `chip--sale-cancelled`.
- `DeleteSaleHandler` permite eliminar permanentemente ventas en estado Cancel.

### Gaps / Problemas identificados

| # | Problema | Impacto |
|---|----------|---------|
| 1 | Solo se pueden cancelar ventas en estado `OnHold (1)`. Las ventas `Paid (2)` no tienen ruta de cancelación. | No cumple criterio de auditoría completa. |
| 2 | Al cancelar una venta Pagada, el `SaleIncome` del cash session queda sin reversar. Los totales de caja quedan incorrectos. | Viola criterio 2 y 3. |
| 3 | No existe `CashMovementType.SaleCancellation`. No hay forma de registrar una reversión visible en el historial. | Viola criterio 3. |
| 4 | `CashSessionMapper.MapSummary()` suma todos los `SaleIncome` sin descontar cancelaciones. | Los totales de caja reflejan ingresos cancelados. |
| 5 | Frontend guarda `idSaleStatus !== 1` → no deja cancelar ventas Pagadas. | Bloquea el flujo completo. |
| 6 | `DeleteSaleHandler` permite borrar ventas canceladas — si el usuario borra después de cancelar, viola el criterio 1. | Riesgo menor (acción separada), pero documentar restricción. |

---

## Decisiones de diseño

### Nueva ruta de cancelación: `POST /api/sales/{id}/cancel`
- Comando separado `CancelSaleCommand` (no extender `UpdateSale`) → Single Responsibility.
- Funciona para **OnHold y Paid**. El estado actual de la venta determina qué efectos secundarios se aplican.
- `UpdateSale` **queda como está** (solo OnHold → otras transiciones).

### Nuevo `CashMovementType.SaleCancellation = 7`
- Dirección: **Out**.
- Solo se registra cuando se cancela una venta en estado **Paid** que tiene ingreso de efectivo.
- El movimiento queda visible en el historial de la sesión de caja.
- `ExpectedClosingAmount` ya descuenta automáticamente movimientos Out → el balance queda correcto.

### Actualización del resumen de caja
- `CashSessionSummaryResponse` agrega campo `salesCancellations` (decimal).
- `salesIncome` permanece inalterado (suma bruta de ingresos).
- El **neto** queda implícito: `salesIncome - salesCancellations`.
- Así el dueño puede ver ambos: ingresos totales Y lo que fue revertido.

### Stock al cancelar una venta Pagada
- La venta pagada ejecutó `ConfirmSaleOut` → el stock salió físicamente.
- Al cancelar, se aplica `ApplyManualEntry` para devolver el stock + registro `StockMovement.SaleReturn`.

---

## Scope Backend — `C:\Eiti\eiti\`

### 1. `eiti.Domain/Cash/CashMovementType.cs`
- Agregar: `SaleCancellation = 7`

### 2. `eiti.Domain/Cash/CashSession.cs`
- Agregar método:
  ```csharp
  public void RegisterSaleCancel(decimal amount, Guid saleId, UserId createdByUserId)
  ```
  - Guard: `EnsureOpen()`
  - Crea movimiento tipo `SaleCancellation`, dirección `Out`.
  - `ReferenceType = "Sale"`, `ReferenceId = saleId`.
  - `Description = "Sale cancellation"`.

### 3. Nueva feature: `eiti.Application/Features/Sales/Commands/CancelSale/`
Archivos a crear:
- `CancelSaleCommand.cs` — `record CancelSaleCommand(Guid Id) : IRequest<Result>, IRequirePermissions`
  - Permiso requerido: `PermissionCodes.SalesDelete` (cancelar es una acción destructiva similar a borrar).
- `CancelSaleErrors.cs` — errores específicos: `NotFound`, `AlreadyCancelled`, `CashSessionNotFound`.
- `CancelSaleHandler.cs` — lógica:

  ```
  1. Obtener venta por Id + validar CompanyId
  2. Si SaleStatus == Cancel → Error AlreadyCancelled
  3. Si SaleStatus == OnHold:
     a. Para cada detail: ReleaseReservation en stock + StockMovement(ReleaseReservation)
     b. Cancelar TransportAssignment si existe
     c. sale.Update(..., SaleStatus.Cancel, ...)
  4. Si SaleStatus == Paid:
     a. Para cada detail: ApplyManualEntry en stock (devolver stock) + StockMovement(SaleReturn)
     b. Si la venta tenía pago en efectivo (cashAmount > 0):
        - Obtener CashSession por sale.CashSessionId
        - session.RegisterSaleCancel(cashAmount, saleId, userId)
     c. Cancelar TransportAssignment si existe
     d. sale.Update(..., SaleStatus.Cancel, ...)
  5. SaveChangesAsync
  ```

### 4. `eiti.Application/Features/CashSessions/Common/CashSessionSummaryResponse.cs`
- Agregar parámetro: `decimal SalesCancellations`

### 5. `eiti.Application/Features/CashSessions/Common/CashSessionMapper.cs`
- En `MapSummary()`:
  ```csharp
  var salesCancellations = session.Movements
      .Where(m => m.Type == CashMovementType.SaleCancellation)
      .Sum(m => m.Amount);
  ```
  - Incluir `salesCancellations` en el `return new CashSessionSummaryResponse(...)`.

### 6. `eiti.Api/Controllers/SalesController.cs`
- Agregar endpoint:
  ```csharp
  [HttpPost("{id:guid}/cancel")]
  public async Task<IActionResult> CancelSale(Guid id, ...)
  ```
  - Dispatcha `CancelSaleCommand(id)`.
  - Retorna `204 No Content` en éxito.

### 7. `eiti.Domain/Stock/StockMovementType.cs` (verificar si existe `SaleReturn`)
- Si no existe, agregar `SaleReturn` para registrar devolución de stock al cancelar venta pagada.

### 8. Migración de base de datos (si aplica)
- EF Core usa ints para enums → `SaleCancellation = 7` no requiere migración de columna, solo validar que no hay constraints que lo bloqueen.
- Si `StockMovementType` necesita nuevo valor, idem.

---

## Scope Frontend — `C:\EiTeFront\eiti-front\`

### 1. `src/app/core/services/sale.service.ts`
- Agregar método:
  ```typescript
  cancelSale(id: string): Observable<void> {
      return this.http.post<void>(`${this.base}/${id}/cancel`, {});
  }
  ```

### 2. `src/app/core/models/cash.models.ts`
- Actualizar `CashSessionSummaryResponse`:
  ```typescript
  export interface CashSessionSummaryResponse {
      // ...campos existentes...
      salesCancellations: number;  // nuevo campo
  }
  ```

### 3. `src/app/features/sales/sales-page.component.ts`
Cambios en la lógica de cancelación:

- **`requestCancelSale()`**: Remover guard `idSaleStatus !== 1`. Ahora acepta OnHold (1) y Paid (2).
  - Si la venta es Paid (2), mostrar advertencia adicional en el modal: "Esta venta fue pagada. Al cancelarla se registrará una reversión en la caja."

- **`confirmCancelSale()`**: Cambiar de `saleService.updateSale(id, {..., idSaleStatus: 3})` a `saleService.cancelSale(id)`.
  - El payload ya no se necesita — el backend maneja todo.

- **Mensaje de confirmación**: Diferenciar entre OnHold y Paid en el texto del modal.

### 4. Vista de sesión de caja (buscar componente cash/cash-session)
- En la lista de movimientos, mostrar `SaleCancellation` (type = 7) con label "Cancelación de venta" y estilo visual de reversa (ej. color rojo / dirección Out).
- En el resumen, mostrar `salesCancellations` si es > 0.

---

## Archivos clave a tocar

### Backend
| Archivo | Acción |
|---------|--------|
| `eiti.Domain/Cash/CashMovementType.cs` | Agregar `SaleCancellation = 7` |
| `eiti.Domain/Cash/CashSession.cs` | Agregar `RegisterSaleCancel()` |
| `eiti.Domain/Stock/StockMovementType.cs` | Verificar/agregar `SaleReturn` |
| `eiti.Application/Features/Sales/Commands/CancelSale/` | Crear 3 archivos nuevos |
| `eiti.Application/Features/CashSessions/Common/CashSessionSummaryResponse.cs` | Agregar campo |
| `eiti.Application/Features/CashSessions/Common/CashSessionMapper.cs` | Actualizar `MapSummary()` |
| `eiti.Api/Controllers/SalesController.cs` | Agregar endpoint POST cancel |

### Frontend
| Archivo | Acción |
|---------|--------|
| `src/app/core/services/sale.service.ts` | Agregar `cancelSale()` |
| `src/app/core/models/cash.models.ts` | Agregar `salesCancellations` |
| `src/app/features/sales/sales-page.component.ts` | Refactorizar cancel flow |
| Componente cash session (buscar) | Mostrar tipo `SaleCancellation` |

---

## Flujo completo post-implementación

```
Usuario cancela venta OnHold
  → POST /api/sales/{id}/cancel
  → CancelSaleHandler: ReleaseReservation stock + sale.Status = Cancel
  → Venta sigue visible en grilla con badge "Cancelada"
  → No hay movimiento de caja (nunca fue pagada)

Usuario cancela venta Paid
  → POST /api/sales/{id}/cancel
  → CancelSaleHandler:
      - Devuelve stock (ApplyManualEntry + SaleReturn movement)
      - session.RegisterSaleCancel(cashAmount, saleId)
        → CashMovement: Type=SaleCancellation, Direction=Out
  → Venta sigue visible en grilla con badge "Cancelada"
  → En caja: se ve el SaleIncome original + el SaleCancellation (Out)
  → ExpectedClosingAmount descontado automáticamente
  → CashSessionSummary.salesCancellations refleja el total revertido
```

---

## Notas adicionales

- **`DeleteSale`** permanece: permite borrar permanentemente una venta ya cancelada (acción administrativa consciente). No es parte del flujo de cancelación — son dos acciones distintas.
- **Permiso de cancelación**: reusar `PermissionCodes.SalesDelete` o crear `PermissionCodes.SalesCancel` si se quiere granularidad. Definir con el equipo.
- **Ventas con pago mixto (efectivo + otro método)**: `RegisterSaleCancel` solo revierte el monto Cash. Los otros métodos (transferencia, tarjeta) no tienen contraparte en caja — solo se cambia el estado de la venta.
- **Cash session cerrada**: si la sesión ya está cerrada cuando se intenta cancelar una venta pagada, `EnsureOpen()` lanzará excepción. Documentar este edge case — posiblemente devolver error específico al usuario.
