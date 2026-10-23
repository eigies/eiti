# Arreglos previos a la cuenta corriente: banco de transferencias y saldo a favor al anular

Rama `claude/cuenta-corriente-proveedores-jkakup` (los dos repos). Son los dos hallazgos previos de la revisión.

## A. El banco receptor de una transferencia de cliente se pierde
`CustomerPayment` no tiene dónde guardarlo: el front lo manda y el backend lo descarta. Consecuencia: el
cierre de caja ("Transferencias por banco") solo cuenta ventas de mostrador, nunca cobros de cuenta corriente.
- [ ] Dominio: `CustomerPayment.TransferBankId` + `SetTransferBank(bankId)` (solo transferencia).
- [ ] EF + migración aditiva (`AddCustomerPaymentTransferBank`, columna nullable).
- [ ] Servicio de recibos: `TransferBankId` opcional por medio; si viene, banco activo y habilitado para
      transferencias (`TransferBankInvalid`), igual que en la venta.
- [ ] Comandos: línea del recibo y cobro suelto (request + comando).
- [ ] Extracto: el medio muestra el banco de la transferencia.
- [ ] Caja: el movimiento del cobro lleva el banco (sesión actual e historial) y el desglose por banco del
      resumen suma los cobros CC por transferencia (neto: ingreso − anulación dentro de la sesión).
- [ ] Auditoría: el snapshot del cobro incluye el banco.
- [ ] Front: desglose por banco y renglón del cobro en caja; detalle del medio en extracto y comprobante.

## B. Anular un pago/cobro pierde el saldo a favor previo
Al registrar un pago, el applicator imputa **todo** el saldo a favor (el previo también) con back-link al
pago. La reversa hace `max(0, monto − imputado)`: si lo imputado supera al pago, el saldo previo desaparece.
Las NC ya lo hacen bien (`AddCredit(imputado); ConsumeCredit(monto)`).
- [x] `SupplierPaymentReversal` y `CustomerPaymentReversal` con la misma cuenta que las NC.
- [x] Tests: saldo previo + pendiente → pago → anular deja saldo y pendientes como antes (proveedor y
      cliente, pago suelto y grupo de varios medios). Fallaban los 4 (se perdían los 50.000); ahora 421/421.

## Verificación
- [ ] `dotnet test` + `ng build` + `ng test`.
- [ ] Postgres real: migración + e2e con transferencia con banco y con saldo previo + `LedgerCheck`.

---

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
- [x] `eiti.Domain/Suppliers/SupplierPaymentOrder.cs`: `Id, CompanyId, SupplierId, BranchId, Code, Date,
      Notes, Status (Active/Cancelled), TotalAmount, CreatedAt, CreatedByUserId, Cancelled*`. El total se
      arma con `AddPayment(pago)` (vincula el pago, exige mismo proveedor y pago activo); `Cancel()` lanza
      si ya está anulada. Que haya ≥ 1 medio lo garantiza el validador del comando.
- [x] `SupplierPayment.PaymentOrderId` (`Guid?`) + `AttachToOrder(orderId)`.
- [x] Espejo en clientes: `eiti.Domain/Customers/CustomerReceipt.cs` + `CustomerPayment.ReceiptId`.
- [x] EF: `IEntityTypeConfiguration` de las dos, `decimal(18,2)`, `HasIndex` en `SupplierId`/`CustomerId`
      y en las FK nuevas, **índice único `(CompanyId, Code)`** para que un choque del `count + 1` falle
      fuerte en vez de duplicar el número.
- [x] Repositorios `ISupplierPaymentOrderRepository` / `ICustomerReceiptRepository` (solo lo que usan
      los handlers: `AddAsync`, `GetByIdAsync` con sus pagos, `CountByCompanyAsync`) + DI.
- [x] Migración `AddPaymentOrdersAndReceipts`.

### Registrar el pago agrupado
- [x] `SupplierPaymentOrderService` (`Features/Suppliers/Common`, inyectable): valida **todos** los medios
      primero (método, monto, cheque en cartera, sin cheques repetidos), después crea la OP y registra cada
      medio con la regla de siempre (caja, endoso, `AddCredit` + FIFO con back-link por medio). Lo usan el
      endpoint viejo y el nuevo: una sola copia de la regla. Errores de medio en `SupplierPaymentLineErrors`
      (mismos códigos `Suppliers.AddPayment.*` de antes).
- [x] `CustomerReceiptService` (ídem clientes: tarjeta + recargo, cheque a cartera, caja, FIFO y
      **confirmación de stock una sola vez** por venta que queda paga). `AddCustomerPaymentHandler` bajó de
      11 a 6 dependencias.
- [x] `AddSupplierPaymentOrder` → `POST suppliers/{id}/payment-orders` (`Date, Notes, Lines[{ Method,
      Amount, ChequeId?, Reference? }]`). Validador: 1–50 medios, `Enum.IsDefined` y **no** `SupplierCredit`,
      montos > 0, cheque obligatorio con método cheque, **sin cheques repetidos**. Un solo `SaveChangesAsync`.
- [x] `AddCustomerReceipt` → `POST customers/{id}/receipts` (líneas con `CardBankId/CardCuotas` y `Cheque`).
- [x] Los endpoints viejos (`POST …/payments`) crean una OP/RC **de un medio**; contrato HTTP sin cambios.
- [x] Auditoría: snapshot del recibo con sus cobros (`AddCustomerReceiptCommand`).
- [x] Tests: 5 medios en una OP (solo el efectivo mueve caja, cheques `Entregado`, FIFO entre facturas),
      excedente a saldo a favor, cheque fuera de cartera / repetido → nada persistido, pago suelto = OP de
      1 medio; recibo con tarjeta + cheque + efectivo, stock confirmado una vez, banco inválido → nada
      persistido; validadores. **398/398.**

### Anular
- [x] `CancelSupplierPaymentOrder` → `DELETE suppliers/{id}/payment-orders/{orderId}` (permiso
      `PurchasesCancel`): revierte **cada medio** con `SupplierPaymentReversal` (cheques a cartera, efectivo
      a caja, compras a pendiente, saldo a favor revertido) y marca la OP anulada. Una sola transacción.
- [x] `CancelCustomerReceipt` → `DELETE customers/{id}/receipts/{receiptId}` (permiso `SalesPay`, igual que
      anular cobro): ídem con `CustomerPaymentReversal` (ventas a pendiente y stock a reservado, cheque anulado).
- [x] Pago/cobro suelto que **comparte grupo** con otros medios → `PartOfOrder` / `PartOfReceipt` (se anula
      el grupo entero). Si era el **único medio**, se revierte y su OP/RC queda anulada con él.
- [x] La misma regla vale para **anular compra / anular venta en modo "revertir los pagos"**: si un pago
      que financió el comprobante es parte de un grupo de varios medios, se rechaza (anular el grupo o
      elegir saldo a favor). Las dos reversas pasan por los servicios: `CancelSupplierPayment` 8 → 7 deps,
      `CancelCustomerPayment` 10 → 7.
- [x] Auditoría: snapshot antes/después del recibo al anularlo.
- [x] Tests: OP de 4 medios anulada (caja, cheques, facturas, saldo a favor), ya anulada / de otro
      proveedor, medio suelto de un grupo rechazado, pago de 1 medio anula su OP; ídem recibo (stock vuelve a
      reservado, cheque anulado). **405/405.**

### Extracto
- [x] `Application/Common/Accounts/LedgerBuilder.cs`: función pura → **saldo anterior**, renglones en orden
      **ascendente** (día de negocio, después instante de registro) con **saldo acumulado**, totales del
      período. Lo anulado no mueve el saldo y aparece solo si se pide. Compartida por proveedores y clientes.
- [x] Fechas: lo guardado como instante (`CreatedAt` de compras/ventas) se ubica en el día del usuario con
      `BusinessCalendar.ToBusinessDate` (nuevo, inverso de `StartOfDayUtc`); lo que ya es fecha de calendario
      (`Date` de pagos, OP/RC, NC) se usa tal cual. El filtro Desde/Hasta compara días contra días.
- [x] Contrato único `AccountLedgerResponse` (rows con `Kind`/`Source`/`Means`/`Imputations` + `Pending`);
      los textos de cada medio los arma el front con datos estructurados (cheque, banco, vto, cuotas, recargo).
- [x] `GetSupplierLedger` → `GET suppliers/{id}/ledger?dateFrom&dateTo&includeCancelled`: compras, OP
      (un renglón por orden), pagos sin orden, **pagos cargados en la compra** (legacy), NC. Las imputaciones
      no son renglones. `pending[]` con antigüedad en días.
- [x] `GetCustomerLedger` → `GET customers/{id}/ledger`: ventas CC, recibos, cobros sin recibo, cobros
      legacy por venta, NC.
- [x] **Control de cuadre** en tests: saldo final del extracto = `Σ pendiente − saldo a favor`.
- [x] Tests: `LedgerBuilder` (ejemplo del cliente 10 + 2 − 8 = 4, mismo día, anulados, rango, saldo a
      favor), `ToBusinessDate`, extracto de proveedor (OP con 4 medios, legacy, pago sin orden, NC, OP anulada,
      Desde con saldo anterior, pendientes) y de cliente (recibo con tarjeta/cheque). **416/416.**
- [x] Herramienta de cuadre **`tools/LedgerCheck`** (solo lectura): corre los mismos handlers de la API
      sobre cada proveedor y cliente con movimientos y lista las cuentas donde el saldo del extracto no da
      `pendiente − a favor`. Probada en Postgres local: 4/4 cuentas cuadran y detecta una diferencia inyectada.
- [ ] **Correrla contra producción antes del release** (necesita la connection string, no la tengo):
      `dotnet run --project tools/LedgerCheck -- "<connection string>"`.
- [x] `GetSupplierAccount` / `GetCustomerAccount` **no se tocaron**.

### Permisos
- [x] Sin permisos nuevos: alta usa `PurchasesPay` / `SalesPay`, extracto `PurchasesAccess` / el de
      cuentas de clientes, anular el mismo que hoy anula pagos. (Si aparece alguno: las **4 listas**,
      dentro de su bloque temático.)

### Tests (`eiti.Tests`) — cubiertos en los pasos de arriba
- [x] `LedgerBuilder`: saldo anterior, orden por fecha y luego `CreatedAt`, NC al haber, anulados sin
      efecto, saldo negativo = a favor.
- [x] `AddSupplierPaymentOrder`: 3 cheques + transferencia + efectivo → 5 pagos con la misma OP, cheques
      `Entregado`, **solo el efectivo mueve caja**, FIFO correcto entre facturas, excedente a saldo a favor,
      cheque repetido rechazado, falla de una línea → nada persistido.
- [x] `AddCustomerReceipt`: tarjeta con recargo + cheque a cartera + efectivo, venta que queda paga
      confirma stock una sola vez.
- [x] `Cancel*`: todas las líneas revertidas, cheques de vuelta en cartera, caja neteada, saldo restaurado.
      Anular una línea suelta de un grupo → `PartOfOrder` / `PartOfReceipt`.
- [x] Endpoint viejo genera OP/RC de una línea (los tests existentes de `AddCustomerPayment` y
      `CancelSupplierPayment` tienen que seguir verdes).

## Frontend (`eiti-front`)

Antes de escribir componentes: skill `/frontend-design` (CLAUDE.md).

**El mockup es la especificación visual**: misma estructura, columnas, textos, colores (vía variables),
espaciados y comportamiento en los tres anchos. Lo único que NO se lleva: el interruptor de tema del
mockup (la app ya tiene `ThemeService`), la página atenuada de fondo del modal (en la app es la pantalla
real) y lo que dependa de las decisiones abiertas de abajo (cheque propio, cuenta propia, elegir facturas).

- [x] Modelos en `core/models/account-ledger.models.ts` (`AccountLedger`, `LedgerRow`, `LedgerMeans`,
      `LedgerImputation`, `PendingDocument`, `LedgerQuery`, `GroupedPaymentLine`, `GroupedPaymentSubmit`, textos
      por cuenta `SUPPLIER_LEDGER_LABELS` / `CUSTOMER_LEDGER_LABELS`) + requests de OP y RC.
- [x] Servicios: `getLedger`, `createPaymentOrder`, `cancelPaymentOrder` / `getLedger`, `createReceipt`,
      `cancelReceipt`. URLs desde `environment.apiUrl`.
- [x] **`app-account-ledger`** (OnPush): Extracto / Pendientes, Desde/Hasta, "Mostrar anulados", renglón
      desplegable con medios e imputación (Saldada / Con saldo), totales, export Excel (números como números)
      y PDF con la marca de la empresa. Outputs: `queryChange`, `paySelected`, `cancelRow`, `printRow`,
      `openDocument`. Formato de fechas sin pasar por `Date` (un `2026-10-02` parseado es UTC).
- [x] **`app-grouped-payment-modal`**: `FormArray` tipado, medio nuevo precargado con lo que falta,
      cheque de cartera entero (no se edita el monto, no se repite), tarjeta con banco/plan/recargo, cheque
      recibido con sus datos, banco receptor de transferencia, total/diferencia/saldo a favor y vista
      previa FIFO. **Confirmar bloqueado mientras cargan cheques o bancos** y dice qué medio falta completar.
      Todo con `app-searchable-select`; fecha con `toLocaleDateString('en-CA')`.
- [x] Responsive: el extracto por **ancho del contenedor** (`@container`: tabla ≥ 1000, compacta, tarjetas
      < 640); el modal por ventana (≤ 1100 pantalla completa en dos columnas, ≤ 760 una columna con
      Confirmar fijo abajo). Oscuro y claro, solo variables de `styles.css`.
- [x] `supplier-account.component` y `customer-account.component` usan los dos componentes; quedan
      cabecera, tarjetas, nota de crédito, detalle de venta, anular venta y remitos. CSS podado (601 → ~150 y
      2219 → ~1330 líneas).
- [x] Specs: componentes nuevos, formato, y los de las dos pantallas. `customer-account.transfer-bank.spec.ts`
      se borró: su caso vive ahora en el spec del modal.
- [x] `audit.component.ts`: etiquetas de los comandos nuevos.
- [x] Comprobante PDF del pago/cobro: código OP/RC en título y archivo, tabla de medios si hay más de uno.

## Verificación
- [x] .NET 10 SDK instalado por apt y `npm ci` en el contenedor.
- [x] `dotnet build` con dependencias + `dotnet test`: **416/416**.
- [x] `ng build --configuration development` sin errores + `ng test` completo: **330/330**.
- [x] **Punta a punta contra PostgreSQL 16 real** (API local, base vacía): la migración aplica sola al
      arrancar; recibo RC-0001 con tarjeta + cheque + efectivo → extracto cliente 300.000 / 400.000 / 75.000;
      OP-0001 con el cheque recibido + transferencia + efectivo → extracto proveedor 500.000 / 800.000 /
      −50.000 (a favor), caja baja **solo** los 250.000 de efectivo, cheque `Entregado`; anular un medio
      suelto → `PartOfOrder`; anular la OP → saldo 800.000, pendientes de vuelta, cheque `EnCartera`, caja
      restituida; pago por el endpoint viejo → OP-0002 de un medio que se anula con él; anular el recibo →
      saldo cliente 400.000, cheque `Anulado`. Corrido dos veces seguidas, verde.
- [x] Visual con Playwright contra `ng serve` y API simulada: escritorio oscuro/claro, celular vertical y
      horizontal, Pendientes, modal en los tres anchos (también scrolleado al fondo), vista de clientes.

## Orden de entrega (un commit por paso, push a la rama)
1. Dominio + migración + repos.
2. Registrars + `AddSupplierPaymentOrder` / `AddCustomerReceipt` + endpoint viejo como grupo de 1 + tests.
3. Anulación de grupos + bloqueo de línea suelta + tests.
4. `LedgerBuilder` + endpoints de extracto + tests.
5. Front: modelos/servicios → `app-account-ledger` → `app-grouped-payment-modal` → proveedores → clientes.
6. Verificación completa + sección "Revisión" acá abajo.

## Revisión

**Entregado:** pasos 1 a 6. Backend y front en `claude/cuenta-corriente-proveedores-jkakup` (sin PR).

**Decisiones tomadas en el camino (a validar con el cliente):**
- Un medio suelto de una OP/RC de varios medios **no se anula solo**: se anula el documento entero. Un pago
  de un solo medio sí, y arrastra su OP/RC. Evita documentos a medio anular.
- **Anular compra / venta en modo "revertir los pagos"** se rechaza si alguno de esos pagos es parte de una
  OP/RC de varios medios: hay que anular el grupo o elegir "saldo a favor".

**Lo que encontré en la revisión visual y corregí:** saldo cortado en las tarjetas del celular, fila de
totales rota en horizontal, código y fecha pegados en "Se imputa a", Confirmar deshabilitado sin explicar
por qué, formulario del cheque recibido apretado en escritorio, y (el más serio) en celular, con varios
medios, el contenido del modal se salía de su fondo y dejaba ver la página de atrás.

**Hallazgos previos a este trabajo (no los toqué):**
- El front manda `transferBankId` en las transferencias de clientes, pero `CustomerPayment` **no lo guarda**:
  el banco receptor que elige el usuario se pierde. Ya pasaba antes con el formulario viejo.
- Si un cobro se imputó usando saldo a favor que ya existía y después se anula, el reverso puede no devolver
  ese saldo previo. Caso borde del reverso existente, no de lo nuevo.
- La nota de crédito de proveedores nunca tuvo grilla en su formulario (ahora sí).

**Antes de pasar a `main`:**
- [ ] Correr `tools/LedgerCheck` contra producción y revisar cada cuenta que difiera.
- [ ] Decisiones 1 a 3 de abajo (cliente).
- Deploy: la migración es **aditiva** (2 tablas + 2 columnas nullable) y se aplica sola al arrancar la API.
  El front se deploya aparte con `vercel deploy --prod`.

## Decisiones abiertas (para el usuario / el cliente)
Estado: **4 aplicada** (`--amber-text` en `styles.css` del front). **1, 2 y 3 pendientes de respuesta del
cliente**; las tres son agregados sobre la base (OP/RC + extracto), no la cambian, así que la base avanza igual.

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
