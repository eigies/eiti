# Cuenta corriente: pago agrupado + extracto con saldo (proveedores y clientes)

**Pedido del cliente:** (1) cargar en **un solo pago** varios medios (efectivo, transferencia, varios
cheques…) en vez de uno por uno; (2) ver la cuenta como un **extracto** cronológico con
Debe / Haber / **Saldo acumulado** renglón por renglón. Igual para proveedores y para clientes.

**Mockup aprobado:** https://claude.ai/artifact/KJEifRusLrkqzthTambZvD (escritorio, celular vertical y
horizontal, oscuro y claro). Pestañas **Extracto** (por defecto) / **Pendientes**.

Ramas: `claude/cuenta-corriente-proveedores-jkakup` en `eiti` y en `eiti-front`.

## Cómo está hoy (lo que condiciona el diseño)

- `SupplierPayment` / `CustomerPayment` = **un medio por pago**. Cada uno ya resuelve todo lo suyo:
  asiento de caja (`RegisterSupplierPaymentExpense` / `RegisterCustomerPaymentIncome`), cheque
  (endoso de cartera / alta en cartera), tarjeta (recargo), imputación FIFO vía
  `SupplierCreditApplicator` / `CustomerCreditApplicator` (filas `PurchasePayment.SupplierPaymentId` /
  `SaleCcPayment`), y su reverso (`SupplierPaymentReversal`, `CancelCustomerPayment`).
- `GetSupplierAccount` / `GetCustomerAccount` devuelven movimientos **del más nuevo al más viejo**, sin
  saldo por renglón. El front los parte en 3 tablas (compras/ventas, pagos/cobros, NC).
- Códigos: `COMP-XXXX` sale de `count + 1` (`CreatePurchaseHandler`).
- `AddCustomerPaymentHandler` ya tiene 11 dependencias (el límite de la casa es ~10).

## Decisión de diseño: el pago agrupado es una **cabecera** sobre los pagos que ya existen

Una entidad nueva `SupplierPaymentOrder` (**OP-0001**) / `CustomerReceipt` (**RC-0001**) agrupa N
`SupplierPayment` / `CustomerPayment` existentes mediante una FK nullable (`PaymentOrderId` / `ReceiptId`).

Por qué así y no "un pago con líneas hijas":
- **Cada línea sigue siendo un pago completo**, así que caja, cheques, tarjeta, imputación FIFO, reverso,
  reportes de medios de pago y arqueo **no se tocan**: ya funcionan por pago.
- Migración **aditiva**: 2 tablas nuevas + 2 columnas nullable. Los pagos viejos quedan con FK `null` y
  se muestran como un grupo de una línea, sin migrar datos.
- FIFO línea por línea da exactamente el mismo resultado que FIFO por el total.

## Backend (`eiti`)

### Dominio + infraestructura
- [ ] `eiti.Domain/Suppliers/SupplierPaymentOrder.cs`: `Id, CompanyId, SupplierId, BranchId, Code, Date,
      Notes, Status (Active/Cancelled), TotalAmount, CreatedAt, CreatedByUserId`. `Create(...)` valida
      total > 0; `Cancel()` lanza si ya está anulada.
- [ ] `SupplierPayment.PaymentOrderId` (`Guid?`) + `AttachToOrder(orderId)`.
- [ ] Espejo en clientes: `eiti.Domain/Customers/CustomerReceipt.cs` + `CustomerPayment.ReceiptId`.
- [ ] EF: `IEntityTypeConfiguration` de las dos, `decimal(18,2)`, `HasIndex` en `SupplierId`/`CustomerId`
      y en las FK nuevas, **índice único `(CompanyId, Code)`** para que un choque del `count + 1` falle
      fuerte en vez de duplicar el número.
- [ ] Repositorios `ISupplierPaymentOrderRepository` / `ICustomerReceiptRepository` (solo lo que usan
      los handlers: `AddAsync`, `GetByIdAsync` con sus pagos, `CountByCompanyAsync`) + DI.
- [ ] Migración `AddPaymentOrdersAndReceipts`.

### Registrar el pago agrupado
- [ ] Extraer la lógica de **una línea** de `AddSupplierPaymentHandler` a `SupplierPaymentRegistrar`
      (`Features/Suppliers/Common`): valida medio, cheque en cartera, asiento de caja, endoso, `AddCredit`
      + FIFO. Lo usan el endpoint viejo y el nuevo, así no hay dos copias de la regla.
- [ ] Ídem `CustomerPaymentRegistrar` (tarjeta + recargo, cheque a cartera, caja, FIFO, **confirmación de
      stock de ventas que quedan pagas**). Esto además baja `AddCustomerPaymentHandler` a menos de 10 deps.
- [ ] `AddSupplierPaymentOrder` (`Command`/`Validator`/`Handler`/`Response`/`Errors`):
      `SupplierId, Date, Notes, Lines[{ Method, Amount, ChequeId?, Reference? }]`.
      Validador: ≥ 1 línea, `Enum.IsDefined`, montos > 0, **sin cheques repetidos**. Handler: auth →
      caja abierta (una vez) → crea la OP → registra cada línea con el registrar → **un solo
      `SaveChangesAsync`** (todo o nada). Responde OP, total, imputaciones, saldo a favor generado.
- [ ] `AddCustomerReceipt`: igual, con `CardBankId/CardCuotas` y `Cheque` por línea.
- [ ] El endpoint viejo (`POST suppliers/{id}/payments`, `POST customers/{id}/payments`) pasa a crear una
      OP/RC **de una línea**, así todo pago nuevo tiene número y el extracto es uniforme. El contrato
      HTTP no cambia.

### Anular
- [ ] `CancelSupplierPaymentOrder`: revierte **cada línea** con `SupplierPaymentReversal` (cheques
      vuelven a cartera, caja se netea, imputaciones se cancelan) y marca la OP anulada. Una sola transacción.
- [ ] `CancelCustomerReceipt`: ídem con la lógica de `CancelCustomerPayment` (extraer a
      `CustomerPaymentReversal` para no duplicarla).
- [ ] `CancelSupplierPayment` / `CancelCustomerPayment` sobre una línea que **pertenece a un grupo** →
      `Failure(PartOfGroup)`: se anula el grupo entero, no una línea suelta (si no, el número de OP queda
      mintiendo). Los pagos viejos sin grupo se siguen anulando como hoy.

### Extracto
- [ ] `Application/Common/Accounts/LedgerBuilder.cs`: función pura que recibe movimientos
      `(fecha, creadoEn, debe, haber, anulado, …)` y un rango, y devuelve **saldo anterior**, renglones en
      orden **ascendente** con **saldo acumulado**, y totales del período. Los anulados salen con saldo
      sin cambio y marcados. Compartida por proveedores y clientes.
- [ ] Rango de fechas con `BusinessCalendar.ToUtcRange` (lección del 15/08: el día del usuario no es el
      día UTC).
- [ ] `GetSupplierLedger` → `GET suppliers/{id}/ledger?dateFrom&dateTo&includeCancelled`:
      `{ openingBalance, rows[], totals, pending[] }`. Cada renglón de pago agrupado trae `lines[]`
      (medio, detalle de cheque/tarjeta, monto) e `imputations[]`. `pending[]` = compras con saldo
      (total, imputado, saldo, días de antigüedad) para la pestaña Pendientes.
- [ ] `GetCustomerLedger` → `GET customers/{id}/ledger`: ídem con ventas CC / cobros / NC.
- [ ] El extracto **no necesita migración ni arreglo de datos**: se calcula al vuelo con lo que ya hay
      (compras/ventas CC, pagos/cobros, NC). Incluir los **pagos legacy por-compra**
      (`PurchasePayment` sin `SupplierPaymentId`, como ya hace `GetSupplierAccount`) y **excluir** las
      imputaciones (`SupplierCredit`/`CustomerCredit`, filas de NC) para no contar dos veces.
- [ ] **Control de cuadre**: saldo final del extracto sin filtro de fechas = `saldoPendiente − saldoAFavor`
      de la tarjeta. Test que lo asegure + query de chequeo sobre todas las cuentas de prod antes del
      release (si alguna no cuadra, es un dato viejo inconsistente para revisar, no se esconde).
- [ ] `GetSupplierAccount` / `GetCustomerAccount` **no se tocan** (siguen alimentando las tarjetas de
      resumen y cualquier otro consumidor).

### Permisos
- [ ] Sin permisos nuevos: alta usa `PurchasesPay` / `SalesPay`, extracto `PurchasesAccess` / el de
      cuentas de clientes, anular el mismo que hoy anula pagos. (Si aparece alguno: las **4 listas**,
      dentro de su bloque temático.)

### Tests (`eiti.Tests`)
- [ ] `LedgerBuilder`: saldo anterior, orden por fecha y luego `CreatedAt`, NC al haber, anulados sin
      efecto, saldo negativo = a favor.
- [ ] `AddSupplierPaymentOrder`: 3 cheques + transferencia + efectivo → 5 pagos con la misma OP, cheques
      `Entregado`, **solo el efectivo mueve caja**, FIFO correcto entre facturas, excedente a saldo a favor,
      cheque repetido rechazado, falla de una línea → nada persistido.
- [ ] `AddCustomerReceipt`: tarjeta con recargo + cheque a cartera + efectivo, venta que queda paga
      confirma stock una sola vez.
- [ ] `Cancel*`: todas las líneas revertidas, cheques de vuelta en cartera, caja neteada, saldo restaurado.
      Anular una línea suelta de un grupo → `PartOfGroup`.
- [ ] Endpoint viejo genera OP/RC de una línea (los tests existentes de `AddCustomerPayment` y
      `CancelSupplierPayment` tienen que seguir verdes).

## Frontend (`eiti-front`)

Antes de escribir componentes: skill `/frontend-design` (CLAUDE.md).

**El mockup es la especificación visual**: misma estructura, columnas, textos, colores (vía variables),
espaciados y comportamiento en los tres anchos. Lo único que NO se lleva: el interruptor de tema del
mockup (la app ya tiene `ThemeService`), la página atenuada de fondo del modal (en la app es la pantalla
real) y lo que dependa de las decisiones abiertas de abajo (cheque propio, cuenta propia, elegir facturas).

- [ ] Modelos en `core/models/account-ledger.models.ts`: `LedgerRow`, `LedgerLine`, `LedgerImputation`,
      `LedgerResponse`, `PendingDocument`, `GroupedPaymentLineDraft`, `GroupedPaymentRequest`.
- [ ] Servicios: `supplier-account.service.ts` (`getLedger`, `createPaymentOrder`, `cancelPaymentOrder`) y
      `customer-account.service.ts` (`getLedger`, `createReceipt`, `cancelReceipt`). URLs desde
      `environment.apiUrl`, errores vía `ToastService`.
- [ ] **`shared/components/account-ledger/`** (`app-account-ledger`, OnPush): pestañas Extracto/Pendientes,
      filtro Desde/Hasta, "Mostrar anulados", renglón de grupo desplegable (medios + imputación),
      totales, export **Excel** (XLSX) y **PDF** (jsPDF). Textos por input (`Compra/Pago/OP` vs
      `Venta/Cobro/RC`). Outputs: `rangeChange`, `paySelected`, `cancelGroup`, `openDocument`.
- [ ] **`shared/components/grouped-payment-modal/`** (`app-grouped-payment-modal`): Reactive Forms con
      `FormArray` de líneas, agregar/quitar medio, total/diferencia en vivo, vista previa FIFO,
      Confirmar. Medios permitidos por input: proveedores = efectivo, transferencia, cheque de cartera;
      clientes = efectivo, transferencia, tarjeta (banco/cuotas/recargo), cheque recibido. Dropdowns con
      `app-searchable-select` (nunca `<select>` nativo). **Confirmar deshabilitado** mientras carguen
      cheques en cartera / pendientes (lección del 08/08). Fecha por defecto con
      `toLocaleDateString('en-CA')`, nunca `toISOString()`.
- [ ] Responsive en los dos componentes: escritorio · **≤ 1100 px** (celular horizontal / tablet: tabla
      compacta, detalle bajo el comprobante, resumen del modal fijo al costado) · **≤ 760 px** (celular
      vertical: tarjetas, modal a pantalla completa con Confirmar fijo abajo). Botones ≥ 44 px.
- [ ] Solo variables CSS de `styles.css`, funcionando en `theme-dark` y `theme-light`.
- [ ] `supplier-account.component`: reemplazar las 3 tablas y el formulario de pago por los dos
      componentes. Quedan cabecera, tarjetas y nota de crédito.
- [ ] `customer-account.component`: ídem. Quedan los modales de detalle de venta y anular venta.
- [ ] Actualizar `supplier-account.component.spec.ts`, `customer-account.component.spec.ts`,
      `customer-account.transfer-bank.spec.ts`.
- [ ] `audit.component.ts`: etiquetas de los comandos nuevos.

## Verificación
- [ ] En este contenedor **no hay `dotnet` ni `node_modules`**: instalar .NET 10 SDK y `npm ci` antes de
      dar nada por hecho.
- [ ] `dotnet build eiti.Application/eiti.Application.csproj` (con dependencias) + `dotnet test` en verde.
- [ ] `ng build --configuration development` sin errores + `npm test` de los specs tocados.
- [ ] Prueba de punta a punta en local: OP con 3 cheques + transferencia + efectivo → extracto cuadra,
      caja solo mueve el efectivo, cheques `Entregado`; anular → todo vuelve. Lo mismo con un recibo de cliente.

## Orden de entrega (un commit por paso, push a la rama)
1. Dominio + migración + repos.
2. Registrars + `AddSupplierPaymentOrder` / `AddCustomerReceipt` + endpoint viejo como grupo de 1 + tests.
3. Anulación de grupos + bloqueo de línea suelta + tests.
4. `LedgerBuilder` + endpoints de extracto + tests.
5. Front: modelos/servicios → `app-account-ledger` → `app-grouped-payment-modal` → proveedores → clientes.
6. Verificación completa + sección "Revisión" acá abajo.

## Decisiones abiertas (para el usuario / el cliente)
1. **Cheque propio** (de chequera de la empresa): hoy **no existe** en el sistema; solo hay cheques de
   cartera (de terceros) que se endosan. El mockup lo muestra. Propuesta: v1 sin cheque propio y
   sumarlo después como entidad aparte (chequera, número, vencimiento, estado debitado).
2. **Transferencia con cuenta bancaria propia**: no hay entidad de cuentas propias; v1 = referencia en texto.
3. **Imputación manual ("Elegir facturas")**: hoy solo FIFO. Propuesta: v1 FIFO; en Pendientes el
   checkbox solo precarga el importe. La imputación dirigida es un cambio en los applicators, aparte.
4. **Ámbar en tema claro**: `#B56D00` no llega a 4.5:1 en texto chico. Propuesta: `--amber-text` nueva
   solo para texto, sin tocar el resto.

---

# Facturación electrónica (EITI ↔ eiti-fiscalization)

**Documentación completa: `docs/facturacion-electronica.md`.** Esto es solo el estado de la tarea.

## Backend — HECHO
- [x] `SaleFiscalDocument` en tabla propia (`Kind`, `Sequence`, `Status`, `ReversedDocumentId`).
      **`Sale` sin una sola columna fiscal**, con test que lo protege.
- [x] `RequestId` determinístico `{saleId}:{inv|cn}:{seq}` — protección contra doble emisión
- [x] Estado `Voided` guardado + transición única en `SaleFiscalDocumentEffects`
- [x] Proyección única `SaleInvoicingView` (6 consumidores, 1 regla)
- [x] `Company/Branch.AutomaticInvoicing` (`sucursal ?? empresa`)
- [x] **1 migración aditiva**: tabla nueva + 2 flags. Cero cambios en `Sales`
- [x] Puerto + cliente HTTP + validador HMAC + DI
- [x] `InvoiceSale`, `ApplyFiscalCallback`, `GetSaleInvoicePdf`
- [x] `FiscalController` (callback anónimo + HMAC sobre raw body), endpoints en `SalesController`
- [x] Permiso `sales.invoice` en las 4 listas, dentro del bloque de ventas
- [x] Trigger automático no bloqueante + opt-in por venta + NC al anular

## Frontend — HECHO
- [x] Modelos, servicio, permiso
- [x] Check "Factura electronica" (solo si la config efectiva no es automática)
- [x] Chip de estado + barra Facturar/Descargar en el detalle
- [x] Config empresa (card 04) y override por sucursal
- [x] **Contrato JSON sin cambios** en todo el refactor: el front no se tocó desde entonces

## Bugs encontrados y corregidos en `eiti-fiscalization`
- [x] No arrancaba en Development (DI vs. registración manual del handler)
- [x] `POST /api/admin/fiscal-profiles` siempre 500 (`nameof(GetAsync)`)
- [x] **Bloqueante**: `provider_response` `jsonb` recibiendo XML → ningún comprobante autorizaba
- [x] Callback exigía HTTPS incluso en loopback → imposible desarrollar
- [x] `issuerCuit` ahora se deriva del perfil fiscal (era un dato fiscal filtrado a EITI)

## Verificación
- [x] EITI **331/331** · fiscalization **47/47** · `ng build` OK
- [x] E2E contra ARCA simulado: emisión sincrónica, encolada + **callback HMAC**, rechazo,
      idempotencia (409), NC asociada, `Voided`, PDF por proxy, servicio caído, reintento

## Pendiente
- [ ] **Commitear** (nada commiteado en los tres repos)
- [ ] Configurar en Railway: `Fiscalization__BaseUrl`, `__ApiKey`, `__CallbackUrl`,
      `__CallbackSigningSecret`. Sin eso la facturación queda apagada y todo lo demás anda igual
- [ ] Alta del perfil fiscal (CUIT, certificado, punto de venta) **en el servicio**
- [ ] Probar contra homologación real de ARCA
- [ ] **Contador**: ¿el recargo por tarjeta se factura? Hoy NO (se factura sobre `TotalAmount`)
- [ ] Limpieza: `fiscalizationdb` local y ventas de prueba en `eitidb` (empresa "distriher")

## Decisiones abiertas a futuro
- Re-facturación: **modelo listo, operación no expuesta** (ver doc §10). Exponerla es un command
  y un botón, sin migración.
