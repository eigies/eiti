# US: Edición de precio unitario al modificar productos en una venta existente

## Historia de Usuario

**Como** usuario del sistema de ventas,
**Quiero** poder editar el precio de un producto cuando realizo un cambio de modelo en una venta ya cargada,
**Para** ajustar el valor final según sea necesario y no depender únicamente del precio predefinido por el sistema.

---

## Análisis de Estado Actual

### Backend — SIN CAMBIOS REQUERIDOS

El backend **ya soporta** precio unitario override en `UpdateSale`. Código relevante:

- `UpdateSaleDetailItemRequest.cs` → tiene `decimal? UnitPrice = null`
- `UpdateSaleHandler.cs` (líneas 137–148) → aplica el override si:
  1. El usuario tiene permiso `SalesPriceOverride`
  2. `UnitPrice` tiene valor y es >= 0

```csharp
// UpdateSaleHandler.cs ~línea 137
if (detail.UnitPrice.HasValue &&
    detail.UnitPrice.Value >= 0 &&
    _currentUserService.HasPermission(PermissionCodes.SalesPriceOverride))
{
    unitPrice = detail.UnitPrice.Value;
}
else
{
    unitPrice = product.Price;
}
```

**Conclusión backend:** No hay trabajo de backend. La API ya acepta y procesa el precio override correctamente.

---

### Frontend — TRABAJO REQUERIDO

**Archivos relevantes:**
- `C:/EiTeFront/eiti-front/src/app/features/sales/sales-page.component.ts`
- `C:/EiTeFront/eiti-front/src/app/features/sales/sales-page.component.html`

**Problema identificado — 2 gaps:**

#### Gap 1: `beginEdit()` no preserva el precio original guardado

```typescript
// sales-page.component.ts ~línea 506-511 (ACTUAL — con bug)
this.editItems = sale.details
    .map(detail => {
        const product = this.findProduct(detail.productId);
        return product ? { product, quantity: detail.quantity, total: detail.totalAmount } : null;
        // ❌ NO popula unitPriceOverride aunque la venta tenía un precio custom
    })
    .filter((item): item is DraftItem => item !== null);
```

**Fix requerido:** Cuando `detail.unitPrice !== productPublicPrice(product)` (o directamente siempre que `canOverridePrice`), asignar `unitPriceOverride: detail.unitPrice`.

#### Gap 2: Al agregar un producto nuevo en modo edición, no hay campo de precio editable

El flujo `addEditItem()` → `addItem()` → `upsertItem()` calcula el total con `productPublicPrice(product)` y no inicializa `unitPriceOverride`. El template no muestra input de precio para los `editItems`.

**Fix requerido:** El template debe mostrar un input de precio por cada `editItem` cuando `canOverridePrice`, igual al patrón existente en `sales-full.component.ts`.

#### Gap 3 (UX): Diferencia entre "carga inicial" y "edición"

En la carga inicial de una venta nueva, el precio se puede editar item por item en `draftItems`. En edición, este comportamiento no existe. La UX debe ser consistente.

---

## Scope de Implementación

### Frontend — `sales-page.component.ts`

1. **`beginEdit()`**: Al mapear `sale.details` a `editItems`, asignar `unitPriceOverride` con el precio almacenado en el detail cuando `canOverridePrice`.
   ```typescript
   return product ? {
     product,
     quantity: detail.quantity,
     total: detail.totalAmount,
     unitPriceOverride: this.canOverridePrice ? detail.unitPrice : undefined
   } : null;
   ```

2. **`setEditItemPrice(item: DraftItem, price: number)`** (método nuevo, análogo al `setDraftItemPrice` existente):
   ```typescript
   setEditItemPrice(item: DraftItem, price: number): void {
     item.unitPriceOverride = price;
     item.total = price * item.quantity;
   }
   ```

3. **`upsertItem()` para modo edición**: Cuando se agrega un producto nuevo via `addEditItem()`, el total inicial ya usa `productPublicPrice` (correcto). El usuario podrá editar el precio en el campo habilitado del template.

4. **`buildRequest()`**: Ya maneja `unitPriceOverride` correctamente:
   ```typescript
   // línea ~1504-1507 (ya correcto)
   ...(canOverride && item.unitPriceOverride !== undefined ? { unitPrice: item.unitPriceOverride } : {})
   ```
   No requiere cambios.

### Frontend — `sales-page.component.html`

Agregar en la sección de `editItems` un input de precio por fila, análogo al existente para `draftItems`. Debe:

- Estar guardado por `*ngIf="canOverridePrice"`
- Usar `(change)` para llamar `setEditItemPrice(item, $event.target.value)`
- Mostrar el precio actual (`item.unitPriceOverride ?? productPublicPrice(item.product)`)
- Recalcular visualmente el `editTotal` (ya reactivo vía `item.total`)

---

## Criterios de Aceptación — Checklist de QA

- [ ] Al abrir edición de una venta con precio custom, el campo precio muestra el precio guardado (no el de catálogo)
- [ ] Al cambiar el producto en edición, el precio se inicializa al precio de catálogo y queda editable
- [ ] Editar el precio actualiza el subtotal del item y el total de la venta en tiempo real
- [ ] Sin permiso `salesPriceOverride`, el campo de precio no se muestra (igual que en creación)
- [ ] El precio enviado al backend en `UpdateSale` refleja el valor editado
- [ ] La venta guardada muestra el precio modificado en el listado

---

## Archivos a Modificar

| Archivo | Tipo de cambio |
|---|---|
| `sales-page.component.ts` | `beginEdit()` + nuevo método `setEditItemPrice()` |
| `sales-page.component.html` | Input precio en sección edit items |

**Archivos que NO se tocan:**
- Ningún archivo de backend
- `sales-full.component.ts` / `.html` (ya funciona correctamente)
- `sale.models.ts` (no hay cambio de modelo)
- `sale.service.ts` (no hay cambio de API)

---

## Notas de Arquitectura

- Seguir el patrón ya establecido en `sales-full.component.ts` (líneas 242–243):
  ```typescript
  get canOverridePrice(): boolean { return this.auth.hasPermission(PermissionCodes.salesPriceOverride); }
  setDraftItemPrice(item: DraftItem, price: number): void { item.unitPriceOverride = price; item.total = price * item.quantity; }
  ```
- El permiso `PermissionCodes.salesPriceOverride` ya existe en `permission.models.ts`
- La interfaz `DraftItem` ya incluye `unitPriceOverride?: number`
- El recálculo del total es inmediato (no hay async necesario): `item.total` es la fuente de verdad para `editTotal`

---

## Referencias de Código

- Backend command: `C:/Eiti/eiti/eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleCommand.cs`
- Backend handler: `C:/Eiti/eiti/eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleHandler.cs` (líneas 137-148)
- Frontend componente: `C:/EiTeFront/eiti-front/src/app/features/sales/sales-page.component.ts`
  - `beginEdit()`: línea ~498
  - `upsertItem()`: línea ~1458
  - `buildRequest()`: línea ~1490
  - `canOverridePrice`: línea ~1481
  - `setDraftItemPrice()`: línea ~1485
- Patrón de referencia (cómo debe quedar): `C:/EiTeFront/eiti-front/src/app/features/sales/sales-full.component.ts` (líneas 242-243)
