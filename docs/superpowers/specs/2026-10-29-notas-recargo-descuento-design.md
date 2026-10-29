# Notas en la venta, recargo de tarjeta en la factura y descuento minorista con tope — diseño

Fecha: 2026-10-29 · Cliente: Baterías Soler · Repos: `eiti` (back) y `eiti-front` (front), ramas `feature/notas-recargo-descuento` salidas de `develop`.

Son tres cambios independientes. Se pueden construir y entregar por separado, en este orden: **1 → 3 → 2**
(el 2 es el más delicado porque cambia el monto que se declara a ARCA).

## Por qué

Pedido del cliente, textual:

> - Algunos clientes tanto minoristas como mayoristas me piden que adjunte notas en la factura por ejemplo al número de presupuesto que corresponde o la patente a veces porque se trata de una batería robada, antes lo agregaba como un ítem, pero ahora tendría que crear un producto para hacer eso.
> - Cuando le vendemos una batería a un cliente la factura que emitimos no tiene incluido el interés del pago con tarjeta de crédito, necesitamos ver una solución de que aparezca en el monto pero que no se compute como ganancia ya que no lo es, te acordás que antes lo teníamos y si le ponía el porcentaje quedaba como un saldo pendiente a abonar.
> - Y si podemos activar un botón de descuento y establecer un porcentaje en las ventas minoristas que quede rastreado con algún reporte para que pueda sacarlo y hacer una trazabilidad de esas ventas.

Respuestas del cliente a las preguntas abiertas:

1. La nota es **una por venta**.
2. Con pago mixto, el interés va **solo sobre lo que se paga en cuotas**.
3. A los mayoristas **no se les cobra con tarjeta**; sería raro. Queda fuera.
4. **Cualquiera** puede hacer descuentos, pero necesita **trazabilidad (un reporte)** y un **porcentaje máximo que fija el dueño** con su usuario Owner.

## Lo que hay hoy (verificado en código de `develop` y en `eiti_e2e`, copia de producción al 28/10/2026)

- **Notas:** `Sale` no tiene ningún campo de texto libre. La factura la dibuja EITI (`GetSaleInvoicePrint` → `invoice-pdf.service.ts` del front) con los ítems de la venta; a ARCA solo viajan totales (`FiscalAmounts`). El presupuesto convertido guarda `Quote.ConvertedSaleId` (20 de 112 presupuestos se convirtieron).
- **Recargo de tarjeta:**
  - Existe: banco + cuotas con `% ` (`BankInstallmentPlan.SurchargePct`) → `Sale.CardSurchargeTotal`, guardado **aparte** de `TotalAmount`. `Sale.EffectiveTotal = TotalAmount + CardSurchargeTotal` y el cobro tiene que cubrir `EffectiveTotal`.
  - `CreateSaleHandler`/`UpdateSaleHandler` calculan el recargo sobre **el subtotal de toda la venta**, una sola vez aunque haya varias tarjetas, sin importar cuánto se paga con tarjeta.
  - La factura se emite por `sale.TotalAmount` (`SaleInvoicingService.BuildAmounts`): **no incluye el recargo**.
  - El reporte de rentabilidad (`SalesReportHandler`) toma el ingreso de los renglones (`SaleDetail.TotalAmount`): el recargo **no** cuenta como ganancia.
  - **Lo que recuerda el cliente:** la única venta minorista con recargo de toda la base es `MATADEROS-033` (23/04): el sistema le exigió $20.608,70 de más y se "cobró" en efectivo justo ese monto. Desde el **07/05 se cobra con el banco "Moura" al 0 %** (duplicado de "MOURA" al 11,06 %): 1.481 pagos con tarjeta, ≈ $243M, todos sin recargo. También existe "BANCO BBVA" al 0 % junto a "BBVA" con recargo. O sea: el recargo está apagado de hecho por un parche de datos.
  - 85 ventas minoristas combinan tarjeta con otro medio: la base del recargo importa.
- **Descuento:**
  - `Sale.GeneralDiscountPercent` existe y la venta CC lo ofrece ("Descuento general %"); la venta minorista **no** tiene ningún control de descuento. `SaleDetail.DiscountPercent` (por renglón) llega por API sin límite; en la base solo lo usan ventas CC (63 ventas, hasta 43,4 %). Ninguna venta minorista tiene descuento.
  - El precio unitario manual ya está protegido por `sales.override_price`.
  - **Bug existente:** `SalesReportHandler` ignora `GeneralDiscountPercent` → una venta con descuento general aparece con ingreso y ganancia inflados. (También ignora `ManualOverridePrice`; ver no-objetivos.)
  - `Sale` no guarda quién la hizo (solo `OverriddenByUserId` para el precio manual).
  - La pantalla Empresa y `UpdateCurrentCompanyCommand` solo piden estar autenticado (sin permiso): cualquier usuario puede cambiar la configuración de la empresa.

---

## 1. Notas en la venta

**Intención:** que el vendedor escriba una observación libre ("Presup. 1234 – Patente AB123CD") y salga impresa en la factura, sin inventar productos.

**Objetivos**
- Una nota de texto libre por venta (`Sale.Notes`, opcional). Vale para ventas minoristas y CC (el pedido menciona ambas).
- Se carga al crear la venta (formulario minorista y formulario CC) y se puede **cargar o corregir después**, en cualquier estado salvo anulada, incluso con la factura ya emitida. *(Mi decisión: la nota no es dato fiscal; no cambia lo que avala el CAE.)*
- Sale impresa en la **factura** (PDF de EITI) en un bloque "Observaciones", y se ve en el **detalle de la venta**.
- Si la venta nace de un presupuesto del sistema, la nota arranca precargada con `Presupuesto <código>` y se puede editar. *(Mi decisión.)* Convertir un presupuesto siempre abre el formulario de venta CC, así que la precarga va ahí.

**Decisiones de cómo**
- Editar la nota de una venta ya creada va por un **comando propio** (`UpdateSaleNotes`), no por `UpdateSale`: `UpdateSale` solo acepta ventas en espera y re-arma detalle, pagos y snapshots de costo (ver lección 2026-09-24 sobre snapshots que se recalculan). Permiso: `sales.update`.
- Largo máximo 500 caracteres; vacío o solo espacios = `null`. *(Mi decisión.)*
- La nota **no** se manda al servicio fiscal. Viaja en `SaleInvoicePrintResponse` para que el front la dibuje.

**No-objetivos**
- Notas por renglón.
- Imprimir la nota en notas de crédito/débito, remito o presupuesto.

---

## 2. Recargo de tarjeta incluido en la factura

**Intención:** la factura tiene que decir lo que el cliente realmente pagó (con el interés de las cuotas), pero ese interés no es ganancia: cubre el costo de financiación de la tarjeta.

**Regla de negocio (la que dio el cliente)**
- El recargo se calcula **sobre la parte de la venta que se paga con esa tarjeta**, con el % del plan (banco + cuotas):
  `recargo = round(base × pct / 100, 2)` con `base` = la parte de la venta que cubre esa línea de tarjeta.
  Lo que pasa por el posnet en esa línea = `base + recargo`.
- **Una sola tarjeta por venta** *(mi decisión)*: `SalePayments` tiene clave `(SaleId, Method)` y el cliente solo pidió el caso mixto (tarjeta + otro medio). El back rechaza un request con más de una línea de tarjeta (hoy las fusiona en silencio y se queda con el banco de la primera) y el front no ofrece una segunda. `Sale.CardSurchargeTotal` = el recargo de esa línea.
- Ejemplo: venta de $100.000 → $40.000 efectivo + resto con MOURA 3 cuotas 11,06 %. Base tarjeta $60.000, recargo $6.636, posnet $66.636. **Factura $106.636.** Cobrado $106.636 → venta pagada, sin saldo pendiente.

**Objetivos**
- **Factura:** total facturado = `TotalAmount + CardSurchargeTotal`, con el IVA contenido a la alícuota de la venta sobre ese total. *(El interés de financiación que cobra el vendedor integra el precio gravado de la operación; si el contador opina distinto, es el único punto a revisar.)* En el PDF, un renglón de ajuste **"Recargo financiero tarjeta (Banco, N cuotas, X %)"** con su monto, para que el detalle cierre con el total.
- **Ganancia:** el recargo **no** entra en ingresos ni ganancia de los reportes de ventas/rentabilidad (como hoy). Los reportes de medios de pago y caja muestran lo cobrado realmente (con recargo), como hoy.
- **Se acaba el "saldo pendiente":** el cobro con tarjeta en cuotas cubre exactamente su base + su recargo, y el front muestra cuánto pasar por el posnet en cada línea.
- **Guarda de factura emitida:** `UpdateSaleHandler` hoy compara `sale.TotalAmount` contra lo facturado. Tiene que comparar `TotalAmount + CardSurchargeTotal`; si no, cambiar las cuotas de una venta facturada dejaría la factura desalineada sin aviso.

**Decisiones de cómo**
- **Contrato del request (mi decisión):** el `Amount` de una línea de tarjeta en `CreateSale`/`UpdateSale` es la **base** (la parte de la venta que paga esa tarjeta, sin recargo). El back es la única autoridad del recargo: busca el plan (`CardSurchargeCalculator.FindPlan`), calcula el recargo y guarda `SalePayment.Amount = base + recargo` y `CardSurchargeAmt = recargo` (se mantiene el significado actual de `SalePayment.Amount` = lo cobrado). El back **no** confía en ningún monto de recargo que mande el front.
- La NC de una venta facturada ya toma los importes de la factura emitida (`GetPrintableDocumentAsync`): anula la factura completa, con recargo, sin cambios.
- Todos los caminos que crean pagos con tarjeta de una venta minorista siguen la misma regla: alta, edición y "Cobrar" desde Gestión de Ventas (todos pasan por `CreateSale`/`UpdateSale`; el front tiene que mandar la base en todos).
  **Falla conocida a arreglar:** el "Cobrar" rápido de Gestión de Ventas reenvía los montos de tarjeta ya guardados (que incluyen el recargo); con el contrato nuevo el recargo se sumaría dos veces. Tiene que mandar la base (`Amount − CardSurchargeAmt`).

**Pendiente de decisión — venta facturada antes de cobrarse y pagada después en cuotas**
Hoy se puede facturar una venta en espera (en Mataderos, 22 de 144 facturas minoristas desde el 01/10 salieron antes del cobro; 3 se cobraron después con tarjeta en cuotas). Con la guarda nueva ese cobro quedaría rechazado, y una venta no puede anular su factura sin anularse entera (la NC solo sale de `CancelSale`). Opciones:
- (a) Cobrar el recargo **sin facturarlo** en ese caso: la factura queda como salió, la venta avisa "recargo no facturado". Chico; esos casos no cumplen el objetivo.
- (b) Emitir **automáticamente una nota de débito** por el recargo asociada a la factura (la ND ya existe para la regularización de NC). Completo; flujo fiscal nuevo, más grande.
- (c) Bloquear el cobro en cuotas con recargo de una venta facturada: hay que anular la venta y rehacerla. Mucha fricción en caja.

**Condición de entrega:** el tratamiento fiscal del recargo está pendiente del contador (`SaleInvoicingService.BuildAmounts`, `docs/facturacion-electronica.md`). El punto 2 se construye último y **no se mergea a `main` sin su OK**. No se agrega un interruptor por empresa.

**No-objetivos**
- Ventas CC y cobros de cuenta corriente con tarjeta (`CustomerPayment`): sin cambios, sin nota de débito.
- Corregir ventas históricas (sin backfill; `MATADEROS-033` queda como está).
- Limpiar los bancos duplicados al 0 %: es un dato que maneja el cliente desde la pantalla Bancos (ver "Puesta en marcha").

---

## 3. Descuento en ventas minoristas con tope y reporte

**Intención:** que cualquier vendedor pueda hacer un descuento en porcentaje sobre la venta minorista, dentro de un máximo que fija el dueño, y que el dueño pueda auditar después qué se descontó, quién y por qué.

**Objetivos**
- En la venta minorista (alta y edición), un control **"Descuento %"** sobre toda la venta, con **motivo opcional** (texto corto). Usa el `GeneralDiscountPercent` que ya existe (se aplica después de los renglones y el recargo sin envío).
- **Tope de la empresa:** `Company.MaxRetailDiscountPercent` (0–100). **Arranca en 0 = descuento deshabilitado** hasta que el dueño fije el tope. *(Mi decisión: el cliente pidió un máximo; sin máximo fijado lo seguro es no permitir.)* El control muestra el máximo ("hasta X %") y no aparece si el tope es 0.
- **Solo el dueño fija el tope:** permiso nuevo `sales.discount.configure` (solo Owner) y comando propio `SetRetailDiscountLimit`, igual que `SetAutomaticInvoicing`. Se edita desde la pantalla Empresa, visible solo con ese permiso. *(No se usa `UpdateCurrentCompany` porque cualquiera puede llamarlo.)*
- **Validación en el back** (venta no CC, en `CreateSale` y `UpdateSale`): `GeneralDiscountPercent` y cada `SaleDetail.DiscountPercent` ≤ tope. Si no, error `Validation` con un `*Errors.cs` propio. En `UpdateSale` se valida solo si el descuento **cambió**, para que bajar el tope no bloquee la edición de una venta en espera que ya lo tenía. *(Mi decisión.)*
- **Trazabilidad en la venta:** `Sale` guarda quién y cuándo fijó el descuento (`GeneralDiscountByUserId`, `GeneralDiscountAt`, mismo patrón que `OverriddenByUserId`/`OverriddenAt`) y el motivo (`GeneralDiscountReason`, opcional, ≤ 200). Se actualizan solo cuando el porcentaje cambia.
- **Factura:** sin cambios; el PDF ya muestra "Bonificación general X %".
- **Falla conocida a arreglar:** el "Cobrar" rápido de Gestión de Ventas no manda `generalDiscountPercent`: al cobrar borraría el descuento. Tiene que reenviar el descuento y su motivo tal como están.
- **Reporte "Descuentos"** en Reportería, con permiso nuevo `reports.sales.discounts` (Owner y los roles que hoy tienen los otros reportes de ventas):
  - Filtros: rango de fechas (con `BusinessCalendar`, ver lección 2026-08-15), sucursal, usuario que aplicó el descuento.
  - Una fila por venta con descuento general: fecha, código, sucursal, cliente, tipo (minorista/CC), aplicado por, %, subtotal antes del descuento, monto descontado, total, motivo.
  - Totales: cantidad de ventas y monto descontado. Excluye ventas anuladas. *(Mi decisión.)* Exportable como los demás reportes.
- **Arreglo del reporte de rentabilidad:** `SalesReportHandler` prorratea el descuento general en cada renglón (`ingreso = renglón × (1 − GeneralDiscountPercent/100)`), para que una venta con descuento no figure con ganancia inflada.

**No-objetivos**
- Tope para ventas CC (siguen sin límite, como hoy).
- Arreglar que el reporte de rentabilidad ignore `ManualOverridePrice` (8 ventas CC): es el mismo tipo de bug, pero va aparte.
- Cerrar el agujero de autorización de `UpdateCurrentCompany`: es real, pero va como cambio propio (misma lógica que la lección 2026-09-24: no mezclar un fix con un cambio de autorización).

---

## Restricciones y reglas del proyecto que aplican

- Un campo nuevo no está terminado hasta que llega **por HTTP**: request del controller + mapeo + test o e2e que lo mande por la API (lección 2026-10-23). Aplica a `Notes`, `GeneralDiscountReason` y al tope.
- Permisos nuevos: `PermissionCodes`, `RoleCatalog`, **`PermissionCatalog.All`**, `permission.models.ts` del front (constante y catálogo), **en su bloque temático** (no al final) y con el prefijo de etiqueta de los vecinos ("Reportería: …"). Reiniciar la API (lecciones 2026-06-02 y 2026-08-03).
- Migración EF única por repo, revisando las que trae `develop` antes de mergear.
- UI: invocar `/frontend-design` antes de tocar pantallas; controles con los componentes de la app (`app-searchable-select`, `toggle-check`), nada nativo (lecciones 2026-07-22 y 2026-10-23). El "Descuento %" se presenta como un botón que despliega el campo, como pidió el cliente.
- Fechas de reporte por `BusinessCalendar`; "hoy" en el front con `toLocaleDateString('en-CA')`.
- Pruebas: unitarias de dominio/handlers (recargo por línea, montos fiscales, guarda de factura, tope, prorrateo) + e2e local. La e2e de facturación va **todo local contra ARCA homologación**, nunca contra el servicio de Railway (producción). Para reproducir con datos reales está `eiti_e2e`.
- Front: `ng build --configuration development` sin errores antes de entregar.

## Puesta en marcha (no es código)

1. **Permisos nuevos, a mano:** los permisos efectivos salen de los perfiles de acceso en la base; `RoleCatalog` solo arma los perfiles de empresas nuevas, y nunca hubo migraciones que asignen permisos a perfiles existentes. *(Mi decisión: se sigue ese antecedente, sin migración de datos.)* En Soler, Emiliano (perfil "Migrado: owner,seller", el único con `users.manage`) se asigna `sales.discount.configure` y da `reports.sales.discounts` a quien corresponda desde Perfiles de acceso.
2. El dueño fija el tope de descuento en Empresa (si no, el descuento no aparece).
3. Para que el recargo se cobre y se facture, los cobros con tarjeta tienen que usar bancos con el % real. Hoy se usa "Moura" al 0 %: hay que desactivarlo (o cargarle los %) y dejar los planes correctos.

## Lo que queda a criterio de quien construye

- Nombres exactos de campos, endpoints y archivos (siguiendo las convenciones del CLAUDE.md).
- Diseño visual de los controles y del reporte, dentro de los patrones de la app.
- Cómo el front de pagos pasa de "monto con recargo" a "base + recargo calculado", manteniendo que la línea de tarjeta muestre cuánto pasar por el posnet.
