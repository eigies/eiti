# Notas de crédito — clientes y proveedores

**Fecha:** 2026-09-05
**Alcance:** ajuste de saldo sin movimiento de mercadería
**Fuera de alcance:** devolución de stock · integración fiscal AFIP

---

## Problema

Hoy el saldo a favor (`Customer.CreditBalance` / `Supplier.CreditBalance`) solo nace de dos
lugares, y los dos tienen un documento de origen:

| Origen | Handler |
|---|---|
| Sobrepago | `AddCustomerPaymentHandler` · `AddSupplierPaymentHandler` |
| Anulación con destino "Credit" | `CancelSaleHandler` · `CancelPurchaseHandler` |

Falta el caso donde **no hay documento de origen**: bonificación posterior, error de
facturación, diferencia de precio acordada. Eso es una nota de crédito.

`CreditBalance` es un acumulador sin historia (`AddCredit` / `ConsumeCredit` suman y restan).
Se banca hoy porque cada movimiento es rastreable hasta su cobro o su documento anulado. Un
ajuste manual sin documento haría crecer el saldo sin que nadie pueda explicar por qué, y el
estado de cuenta no lo mostraría: `Movements` solo conoce `"venta"|"cobro"` y `"compra"|"pago"`.

Por eso la NC es una entidad con su propio tipo de movimiento, no un `AddCredit()` suelto.

---

## Decisión de arquitectura

Dos entidades espejo, una por lado. La NC **origina** el crédito; distribuirlo, imputarlo FIFO
y revertirlo ya funciona y está probado en producción.

```
CustomerCreditNote → customer.AddCredit() → CustomerCreditApplicator (FIFO) → SaleCcPayment
SupplierCreditNote → supplier.AddCredit() → SupplierCreditApplicator (FIFO) → PurchasePayment
```

### Por qué no un método de pago

Sumar `NotaCredito` a `SalePaymentMethod` sería ~20 líneas, y es la opción descartada.

Una NC no es plata que entró. Como método de pago aparecería en el reporte de medios de pago,
en los totales de cobranza y en todo lo que sume `SalePayments`, inflando ingresos que nunca
existieron. Es el bug de SMA-042 otra vez (ver `.claude/rules/lessons.md`, 2026-07-24).

### Por qué no una entidad compartida

`Customer` y `Supplier` son agregados distintos, con repositorios, permisos y slices propios.
Todo el dominio está construido como dos espejos separados. Una entidad con discriminador
cliente/proveedor pelearía contra esa arquitectura para ahorrar un archivo.

---

## El problema de trazabilidad (lo que hay que resolver sí o sí)

`CustomerCreditApplicator` acepta un `customerPaymentId` opcional que termina en
`SaleCcPayment.CustomerPaymentId`, y `Sale.RevertCustomerCredit(id)` deshace las imputaciones
buscando por ese campo.

`ApplyCustomerCreditHandler` lo llama con `customerPaymentId: null`, y el applicator hace
`customerPaymentId ?? Guid.Empty`. **Las imputaciones de ese camino quedan todas con
`Guid.Empty`**: no se pueden revertir de a una, porque revertir por `Guid.Empty` las tocaría
todas juntas, de cualquier origen.

Es un problema preexistente. **Queda fuera de alcance**, pero define el diseño de la NC: si la
NC imputa pasando `null`, sus imputaciones nacen anónimas y "anular NC" no puede deshacerlas.
Sería el patrón de "cancelar el padre deja hijos activos" que ya nos pasó tres veces
(transporte, cobros CC, pagos de proveedor).

**Decisión:** la NC lleva su propio back-link.

- `SaleCcPayment` suma `CreditNoteId` (`Guid?`), hermano de `CustomerPaymentId`.
- `PurchasePayment` suma `CreditNoteId` (`Guid?`), hermano de `SupplierPaymentId`.

Se evaluó generalizar a `SourceType` + `SourceId` (el patrón que ya usan `StockMovement` y
`CashMovement`). Es más limpio a largo plazo, pero obliga a migrar las filas existentes y a
tocar los dos `Revert*`, los dos applicators y cada consulta sobre `CustomerPaymentId` /
`SupplierPaymentId`. Contra el principio de impacto mínimo del CLAUDE.md, se elige el campo
hermano. Si algún día aparece un tercer origen, ahí conviene generalizar.

---

## Entidades

Mismo molde que `CustomerPayment` / `SupplierPayment`.

```csharp
public sealed class CustomerCreditNote
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }

    public string Code { get; private set; }          // NCC-001, interno
    public decimal Amount { get; private set; }        // > 0
    public string Reason { get; private set; }         // requerido, 250
    public DateTime Date { get; private set; }

    public Guid? SaleId { get; private set; }          // venta asociada, opcional
    public CreditNoteStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
}
```

`SupplierCreditNote` es idéntica cambiando `CustomerId`→`SupplierId`, `SaleId`→`PurchaseId` y
el prefijo a `NCP-`.

```csharp
public enum CreditNoteStatus { Active = 1, Cancelled = 2 }
```

**`Reason` es requerido.** Es la única defensa contra un saldo que crece sin explicación: sin
documento de origen, el motivo escrito ES la trazabilidad.

**`Amount` se guarda, no el delta del saldo.** Si mañana hay que abrir neto e IVA, se deriva
del importe igual que hace `Sale.SetVat()`. Guardando solo "el saldo bajó 50.000" esa cuenta
ya no se puede reconstruir.

**`Code` es interno y queda separado de un futuro número fiscal.** El día de AFIP se agrega el
número fiscal al lado, sin reescribir las NC ya emitidas.

### Numeración

`NCC-001` por sucursal, con el mismo esquema que ventas y compras
(`CountByBranchAsync() + 1`, `PadLeft(3, '0')`).

Ese esquema no es AFIP-válido: es un conteo, no una secuencia, así que se repite si se borra un
documento y dos emisiones simultáneas pueden sacar el mismo número. Es deuda técnica que ya
existe en `CreateSaleHandler` y en `CreatePurchaseHandler`. La NC **la hereda a propósito**:
resolverla bien es parte del proyecto fiscal y hay que hacerla en los tres lugares a la vez.

---

## Flujo: emitir

`CreateCustomerCreditNoteHandler` (espejo: `CreateSupplierCreditNoteHandler`).

1. `EnsureAuthenticatedWithContext()`
2. Cliente existe y es de la empresa → si no, `NotFound`
3. Si viene `SaleId`: la venta existe, es del cliente, y no está `Cancel`
4. Numerar y crear la `CustomerCreditNote`
5. `customer.AddCredit(amount)`
6. `CustomerCreditApplicator.ApplyToPendingCcSalesAsync(..., creditNoteId: note.Id)`
7. Confirmar stock de las ventas que pasaron a `Paid` (mismo bloque que
   `ApplyCustomerCreditHandler`)
8. `session.RegisterCustomerCreditNote(...)` — movimiento neutro en caja (ver más abajo)
9. `SaveChangesAsync`

La respuesta devuelve el código, el importe, las imputaciones FIFO y el saldo a favor
resultante — mismo shape que `AddCustomerPaymentResponse`.

### Del lado proveedor

`SupplierCreditApplicator.ApplyToPendingPurchasesAsync` ya recibe `excludePurchaseId`. La NC de
proveedor lo pasa en `null` (no hay compra que excluir) y suma `creditNoteId`.

---

## Caja

La NC **se ve en caja**, como movimiento neutro. Es un hecho comercial del día que el operador
tiene que ver en su sesión, aunque no haya entrado ni salido un peso.

El mecanismo ya existe y se usa: `CashMovementDirection.None`. `ExpectedClosingAmount` lo suma
como `0m` (`CashSession.cs` L594-602), así que el movimiento aparece en el listado y en el PDF
sin tocar el arqueo. Es el mismo tratamiento que `RegisterCcNonCashIncome` le da a un pago CC
con cheque: visible, trazable, fuera del efectivo esperado.

Cuatro tipos nuevos en `CashMovementType`, siguiendo el par emisión/anulación que ya usan
`SaleIncome`/`SaleCancellation` y `PurchaseExpense`/`PurchasePaymentCancellation`:

```csharp
CustomerCreditNote = 19,
CustomerCreditNoteCancellation = 20,
SupplierCreditNote = 21,
SupplierCreditNoteCancellation = 22
```

Cuatro métodos nuevos en `CashSession`, todos con `Direction.None`, junto a los
`RegisterCustomerPayment*` / `RegisterSupplierPayment*` que ya existen. `ReferenceType` nuevo
en `CashReferenceTypes` (`"CreditNote"`), `ReferenceId` = id de la NC.

**Sesión abierta:** la emisión exige sesión abierta en la sucursal, igual que un cobro. Si no
hay, falla con `CashSessionRequired` — el mismo error que ya devuelven los cobros CC.

**Front (`cash.component.ts`):** las cuatro etiquetas en `translateType` y sus colores de badge,
junto a `CuentaCorrienteIncome` y `PurchaseExpense`. Al ser `None`, el operador ve el
movimiento con importe pero sin flecha de entrada ni de salida.

Una NC visible en caja también cierra un agujero de control: hoy una bonificación se aplica sin
dejar rastro en la sesión del día, y nadie se entera hasta que alguien mira el estado de cuenta.

---

## Flujo: anular

`CancelCustomerCreditNoteHandler` (espejo: `CancelSupplierCreditNoteHandler`).

**No se reutiliza `CustomerPaymentReversal.ReverseAsync`.** Esa función reintegra efectivo al
cajón y devuelve el cheque a cartera. Una NC no movió efectivo ni cheque: su reversa en caja es
otro movimiento neutro, no un reintegro. Pasarle una sesión nula "para que no haga nada"
acopla la NC a una firma que no le corresponde.

Lo que sí se reutiliza es lo que ya existe en el dominio:

1. `ListByCreditNoteIdAsync` trae las ventas que la NC financió
2. `sale.RevertCreditNote(noteId)` — hermano de `RevertCustomerCredit`, misma lógica
3. Las ventas que estaban `Paid` vuelven a pendiente y el stock vuelve a reservado
4. El crédito no consumido se revierte: `customer.ConsumeCredit(amount - imputedTotal)`
5. `session.RegisterCustomerCreditNoteCancellation(...)` — neutro, como la emisión
6. `note.Cancel()`

**Si el crédito ya se consumió, anular la NC deja las ventas impagas otra vez.** Es lo
correcto: la plata nunca existió.

**Caso borde que hay que cubrir:** si el cliente gastó el saldo en una venta que después se
anuló, `ConsumeCredit` podría dejar `CreditBalance` negativo. El handler valida antes y falla
con `CreditAlreadyConsumed` si el saldo disponible no alcanza, en vez de romper la invariante.

---

## Estado de cuenta

`GetCustomerAccountResponse` hoy tiene `DeudaTotal` / `CobradoTotal` / `SaldoPendiente` /
`SaldoAFavor`, y `CustomerAccountMovement.Type` es `"venta"|"cobro"`.

**La NC baja la deuda, no sube lo cobrado.** No entró un peso. Si fuera a `CobradoTotal`, el
estado de cuenta diría que cobraste plata que nadie pagó.

- `Type` suma un tercer valor: `"nota_credito"`
- `IsDebit = false`
- Resta de `SaldoPendiente`, **no** suma a `CobradoTotal`
- `Description` muestra el `Reason`
- `Imputaciones` se llena igual que un cobro: qué ventas cubrió y por cuánto

`SupplierAccountMovement` idem, con `"nota_credito"` junto a `"compra"|"pago"`.

`DeudaTotal` se mantiene como el bruto facturado. La NC se ve en su propia fila y en el
`SaldoPendiente`, que es donde el usuario mira.

---

## Permisos

Cuatro códigos, cada uno en el bloque temático que ya existe — no al final de la lista
(`.claude/rules/lessons.md`, 2026-08-03).

| Código | Bloque |
|---|---|
| `sales.credit_note.create` | junto a `sales.*` (`PermissionCodes.cs` L5-9) |
| `sales.credit_note.cancel` | ídem |
| `purchases.credit_note.create` | junto a `purchases.*` (L74-78) |
| `purchases.credit_note.cancel` | ídem |

Las cinco listas: `PermissionCodes.cs`, `RoleCatalog.cs`, `PermissionCatalog.All`, y en el
front `permission.models.ts` (mapa `PermissionCodes` + array `PermissionCatalog`). Etiquetas
con el prefijo de sus vecinas.

**Reiniciar la API** después: `PermissionCatalog.All` es un set estático leído en memoria.

---

## Frontend

Las dos pantallas ya existen; no hay ruta nueva.

| Pantalla | Cambio |
|---|---|
| `clients/customer-account` | Botón "Nota de crédito" + modal + fila nueva en el estado de cuenta |
| Cuenta de proveedor (`purchases/supplier/:supplierId`) | Ídem |

El modal pide importe, motivo (requerido), fecha y venta/compra asociada (opcional). Usa
`app-searchable-select` para elegir el documento — nunca un `<select>` nativo
(`.claude/rules/lessons.md`, 2026-07-22).

Anular pasa por `ConfirmationService` mostrando el importe y advirtiendo si el crédito ya se
consumió, porque en ese caso hay ventas que vuelven a impagas.

Servicios nuevos en `core/services/`, modelos en `core/models/`, sin lógica de negocio en los
componentes.

---

## Tests

**Dominio**
- `Amount <= 0` lanza; `Reason` vacío lanza
- `Cancel()` dos veces lanza
- `Sale.RevertCreditNote` deshace solo las filas de esa NC, no las de otra ni las de un cobro

**Emisión**
- Suma al `CreditBalance` y se imputa FIFO a la venta más vieja
- Excedente queda como saldo a favor
- Sin ventas pendientes, todo queda como saldo a favor
- Con `SaleId` de otro cliente → `NotFound`
- Sin sesión de caja abierta → `CashSessionRequired`

**Caja**
- Genera un `CashMovement` con `Direction.None`
- **`ExpectedClosingAmount` no cambia** antes y después de emitir la NC — el arqueo tiene que
  dar exactamente lo mismo. Es el test que importa de este bloque
- Anular genera el movimiento de cancelación, también neutro

**Anulación**
- Revierte solo sus propias imputaciones (con un cobro y otra NC en la misma venta)
- La venta vuelve de `Paid` a pendiente y el stock a reservado
- Sin saldo suficiente → `CreditAlreadyConsumed`, sin dejar `CreditBalance` negativo

**Estado de cuenta**
- La NC aparece como `"nota_credito"`, baja `SaldoPendiente` y **no** toca `CobradoTotal`

El test que importa es el de "revierte solo sus propias imputaciones": es el que falla si
alguien vuelve a pasar `null` como back-link.

---

## Qué queda abierto para AFIP

Nada de esto es de la NC — son huecos que ya existen en `Sale` y `Company`, del mismo tamaño
se haga la NC o no:

1. Numeración por conteo, no correlativa por punto de venta y tipo de comprobante
2. `Company` sin CUIT, punto de venta ni condición frente al IVA
3. Sin condición de IVA del receptor (define comprobante A/B/C)
4. IVA no discriminado por alícuota (`Sale` tiene un `VatRate` único derivado del total)
5. Sin CAE: número, vencimiento y estado del pedido

Lo que esta NC sí deja resuelto para ese día: la NC es un comprobante con identidad propia
(AFIP tipo 3/8/13), con referencia al comprobante asociado (`CbtesAsoc`) y numeración separada
del futuro número fiscal.
