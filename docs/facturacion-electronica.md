# Facturación electrónica — lado EITI

Cómo EITI se integra con **`eiti-fiscalization`** (repo aparte), y **por qué** está hecho así.
Si vas a tocar algo de esto, leé primero las decisiones: varias parecen arbitrarias y no lo son.

---

## 1. La frontera

> **EITI manda hechos** ("se hizo esta venta"). El servicio fiscal toma **decisiones fiscales**
> (tipo de comprobante, numeración, CAE, QR). **EITI nunca sabe que ARCA existe.**

Consecuencias prácticas:

- **Ningún dato fiscal del emisor vive en EITI.** CUIT, certificado, punto de venta y ambiente están
  en el perfil fiscal del servicio, resueltos por `tenantId`.
- `tenantId` = **`CompanyId` de EITI**. Es la única homologación entre los dos sistemas.
- El único punto de contacto es el **contrato REST**. No hay base compartida.

### Por qué no existe `IssuerCuit` en la configuración de EITI

Hubo una versión que lo tenía como env var, para armar el comprobante asociado de la nota de
crédito. **Estaba mal por dos motivos**: filtraba un dato fiscal a EITI, y —peor— EITI es
**multitenant**: un único env var global significa que con dos clientes facturando, el segundo
emite notas de crédito con el CUIT del primero.

El servicio fiscal ya conoce el CUIT (está en el perfil del tenant), así que lo deriva él.
`AssociatedDocument.IssuerCuit` es opcional del lado del servicio y EITI no lo manda.

---

## 2. El modelo: `SaleFiscalDocument`

**`Sale` no tiene ni una columna de facturación.** Una versión anterior le agregó 13 campos
(`Cae`, `FiscalNumber`, `CreditNoteCae`, `CreditNoteNumber`, …) y estaba mal: le cargaba a la venta
una responsabilidad que no es suya, y la duplicación factura/NC era la señal de que faltaba una
entidad.

Hoy los comprobantes viven en `SaleFiscalDocuments`, tabla y agregado propios:

| Campo | Para qué |
|---|---|
| `SaleId` | A qué venta pertenece el comprobante |
| `Kind` | `Invoice` / `CreditNote` — el discriminador que evita duplicar columnas |
| `Sequence` | Nº de intento por venta y tipo. **De acá sale la idempotencia** (§3) |
| `Status` | `InProgress` / `Invoiced` / `Rejected` / `Voided` |
| `ReversedDocumentId` | Solo en una NC: **la factura que anula** (§4) |
| `FiscalDocumentId` | Id del comprobante en el servicio fiscal (clave del callback) |
| `DocumentType`, `PointOfSale`, `Number`, `AuthorizationCode`, `AuthorizationExpiry`, `QrUrl` | Espejo de lo que resolvió el fisco |

**`Sale` no guarda ni siquiera un FK al comprobante.** Es deliberado: el FK en el padre es
exactamente el patrón que produjo el bug de transporte documentado en `.claude/rules/lessons.md`
(se limpia el FK, queda el registro huérfano, el chip miente). Los listados cargan en lote con
`ListBySaleIdsAsync`, sin N+1.

Hay un test (`SaleHasNoFiscalResponsibilityTests`) que **falla** si alguien vuelve a filtrar un
miembro `Invoic*`/`Fiscal*`/`Cae*` dentro de `Sale`.

---

## 3. Idempotencia: lo más importante de todo

**Un CAE no se puede des-emitir.** Si se emiten dos comprobantes por la misma venta, la única
salida es emitir una nota de crédito. Por eso la protección contra duplicados es la decisión
central del diseño.

El `RequestId` que ve el servicio fiscal es **determinístico**, nunca aleatorio:

```
{saleId}:inv:1    primera factura
{saleId}:inv:2    reintento tras rechazo, o re-facturación
{saleId}:cn:1     nota de crédito
```

La clave está en **cuándo avanza la secuencia**: solo cuando el intento anterior quedó
**rechazado**. Si hay un intento `InProgress`, se **re-envía ese mismo** con su mismo `RequestId`.

Por qué importa:

| Situación | Qué pasa |
|---|---|
| Se pierde la respuesta y el usuario reintenta | Mismo `RequestId` → el servicio devuelve el comprobante que ya emitió. **No duplica** |
| Doble click simultáneo | Ambos calculan la misma secuencia → el índice único del servicio deduplica |
| Rechazo del fisco y reintento | Secuencia +1 → nuevo `RequestId` → nueva emisión, correcto |

> ⚠️ **Si algún día cambiás cómo se arma el `RequestId`, estás tocando la protección contra
> doble facturación.** Un Guid random acá rompe todo en silencio: solo se nota cuando se cae la red.

### "No sé qué pasó" NO es "no se emitió"

Es la otra mitad de la protección, y es fácil de romper sin darse cuenta:

| Resultado | ¿Se emitió? | Estado | Por qué |
|---|---|---|---|
| El fisco **rechazó** | **No**, con certeza | `Rejected` | El reintento puede usar secuencia nueva |
| **Timeout / caída / respuesta perdida** | **No se sabe** | `InProgress` | El reintento **debe** re-enviar el mismo `RequestId` |

Marcar un resultado desconocido como `Rejected` hace que el reintento avance de secuencia y emita
un **segundo comprobante real**. Por eso existe `MarkUnconfirmed`, separado de `Reject`.

**No se intenta distinguir** "no llegó a salir" de "salió y se perdió la respuesta": el riesgo es
asimétrico. Equivocarse hacia trámite cuesta un reintento; hacia rechazo cuesta un comprobante
fiscal duplicado, que solo se arregla con otra nota de crédito.

Corolario: **un intento `InProgress` SE PUEDE reintentar**, y es importante que se pueda — es el
camino de recuperación. `InvoiceSaleHandler` no lo bloquea: bloquearlo dejaba la venta trabada en
"en trámite" para siempre.

Y como respaldo, el callback busca la fila **también por `RequestId`**
(`SaleFiscalDocument.TryParseRequestId`), para el caso en que nunca se llegó a registrar el id que
asignó el servicio fiscal.

Del lado de EITI el índice único `(SaleId, Kind, Sequence)` refuerza lo mismo.

---

## 4. La nota de crédito anula una FACTURA, no una venta

Fiscalmente una NC referencia un **comprobante** — es lo que ARCA asocia en `CbtesAsoc`
(`Tipo + PtoVta + Nro + Cuit + Fecha`). Por eso `ReversedDocumentId` apunta a la factura.

`SaleId` se queda igual en las dos filas: son **dos hechos distintos y los dos verdaderos**.
`SaleId` dice a qué venta pertenece el comprobante; `ReversedDocumentId` dice a cuál anula.

`CreateCreditNote` exige una factura **autorizada**, así que el vínculo no se puede armar mal.

---

## 5. La anulación es un ESTADO, no una deducción

Cuando la NC queda autorizada, la factura pasa a **`Voided`**.

Hubo una versión que **deducía** la vigencia recorriendo las NC y armando un set de anuladas. El
problema no era el rendimiento: era que esa regla había que aplicarla en **cuatro consumidores**
distintos, o sea cuatro oportunidades de olvidarla, para siempre.

Hoy "factura vigente" es un filtro por estado, y la transición vive en **un solo lugar**:
`SaleFiscalDocumentEffects.ApplyAsync`. Se llama desde los **dos** caminos por los que una NC puede
autorizarse —el sincrónico y el callback diferido— para que el comportamiento sea idéntico.

### Una sola proyección de lectura

Todo lo que necesite saber el estado de facturación de una venta usa
**`SaleInvoicingView.From(documents)`**:

| Propiedad | Qué contesta |
|---|---|
| `Status` | Estado a mostrar |
| `Invoice` | Factura a mostrar (vigente, o el último intento para que un rechazo no desaparezca) |
| `LiveInvoice` | Factura **operable**: se le puede emitir NC |
| `PrintableInvoice` | Factura **imprimible**: vigente **o anulada** (§6) |
| `CreditNote` | Última nota de crédito |
| `CanInvoice` | Si se puede emitir |

**No agregues una pregunta nueva resolviéndola en el handler.** Si falta algo, va acá.

---

## 6. Una factura anulada se puede reimprimir

`PrintableInvoice` incluye las `Voided`. Una factura anulada **existió, tuvo CAE y es un documento
legal**: hay que poder reimprimirla. Que esté anulada lo comunica el estado, no la ausencia del PDF.

El PDF se sirve **proxeado por EITI** (`GET /api/sales/{id}/invoice/pdf`), no directo desde el
servicio fiscal: ese endpoint exige la API key de servicio y el navegador no la tiene (ni debe).
El scoping por empresa se valida acá, que es quien sabe de qué empresa es la venta.

---

## 6.1 Aislamiento multitenant

**Toda lectura al servicio fiscal lleva `tenantId`** (`GetDocumentAsync`, `DownloadPdfAsync`), y el
servicio lo usa en la consulta: un documento de otro tenant responde 404.

Dos detalles que importan:

- El `tenantId` **sale del dato que ya validamos**, nunca del input externo. En el callback se toma
  de `document.CompanyId` —la fila que ya localizamos— y no del cuerpo del mensaje: ese cuerpo es
  input de afuera y no puede elegir de qué empresa se leen datos.
- En el proxy del PDF sale del `CompanyId` del usuario autenticado, después de verificar que la
  venta es suya.

EITI ya scopeaba por empresa antes de proxear; el cambio es que ahora **el servicio no depende de
que lo hagamos bien**. La API key es una sola y compartida entre todos los tenants, así que sin
esto el aislamiento entre clientes quedaba delegado por completo en el cliente.

---

## 6.2 Quién conoce el estado real

Hay tres niveles, y cada uno puede atrasarse respecto del siguiente:

| Nivel | Quién | Se atrasa cuando |
|---|---|---|
| 1 | Espejo en EITI (`SaleFiscalDocuments`) | se pierde el callback |
| 2 | Registro del servicio fiscal | se cortó la comunicación con el organismo |
| 3 | **El organismo (ARCA)** | nunca — es la verdad fiscal |

**El front siempre lee el nivel 1.** No consulta al servicio: el listado necesita el estado de N
ventas de una, y una caída del servicio fiscal no puede voltear la pantalla de ventas.

**El nivel 1 se sincroniza** por el callback, y como red de seguridad por el botón "Consultar
estado" (que re-envía el mismo `RequestId` y trae el estado actual del servicio — no emite nada).

**El nivel 2 se sincroniza solo**: el servicio corre un worker de reconciliación cada 15 minutos
que le pregunta al organismo por los comprobantes con número reservado y sin CAE. Es automático
porque la consulta es de solo lectura; no depende de que nadie apriete nada.

Lo único manual es la DLQ del servicio: un comprobante que agotó reintentos *y* que el organismo
confirma que nunca emitió. Ahí decide una persona.

---

## 7. La facturación NUNCA bloquea la operación

Ni la venta ni la anulación. Si el servicio fiscal está caído:

- **Crear la venta**: se completa igual; el comprobante queda `Rejected` con el motivo real, y el
  usuario reintenta desde el detalle.
- **Anular la venta**: se completa igual; la NC queda `Rejected` y se reintenta.

Anular una venta es una decisión del negocio: no puede quedar rehén de la disponibilidad de un
tercero. `CancelSaleHandler` llama a `IssueCreditNoteAsync` y **descarta deliberadamente** el error.

**Si `Fiscalization:BaseUrl` o `ApiKey` están vacíos, `IsEnabled` es `false`** y todo el sistema
funciona exactamente como antes de que esto existiera. Es seguro deployar sin el servicio arriba.

---

## 8. El callback

`POST /api/fiscal/callback` — **anónimo respecto del JWT a propósito**: no lo dispara una persona.
Se autentica con la **firma HMAC**, que solo puede producir quien conoce el secreto compartido.

- `HMAC-SHA256` sobre `"{timestamp}.{rawBody}"`, hex, header `X-Signature: sha256=<hex>`.
- Se firma el **body crudo**: se lee `Request.Body` a bytes y se valida **antes** de deserializar.
  Si se dejara que ASP.NET deserialice y después se re-serializara, la firma no cerraría nunca.
- Anti-replay: `X-Timestamp` dentro del HMAC, ventana ±5 min.
- Rotación sin downtime con `CallbackSigningSecretPrevious`.
- **Sin secreto configurado se rechaza todo.** Nunca "dejar pasar porque no puedo verificar".

### Por qué el callback dispara un `GET` adicional

El callback **no trae** `type`, `pointOfSale` ni `validUntil`. Sin esos tres, una venta autorizada
por vía diferida no podría después emitir su nota de crédito (el comprobante asociado los exige).
Por eso `ApplyFiscalCallbackHandler` consulta el comprobante completo con `GetDocumentAsync`.
Es el patrón de webhook fino: la notificación avisa, el registro autoritativo se consulta.

Se responde **200 aunque no se encuentre la venta**: el servicio reintenta 8 veces con backoff y no
tiene sentido hacerlo por algo que nunca va a resolver.

---

## 9. Configuración

| Clave | Nota |
|---|---|
| `Fiscalization:BaseUrl` | Vacío ⇒ facturación desactivada |
| `Fiscalization:ApiKey` | Va en `X-Api-Key`. **Siempre por env var** |
| `Fiscalization:Regime` | Discriminador multi-régimen (`AR-ARCA`) |
| `Fiscalization:DefaultPointOfSale` | Si la venta no define uno |
| `Fiscalization:CallbackUrl` | URL pública de EITI |
| `Fiscalization:CallbackSigningSecret` | **Idéntico** al `CALLBACK_SIGNING_SECRET` del servicio |
| `Fiscalization:CallbackSigningSecretPrevious` | Para rotar |

En producción **todo por env vars de Railway** (`Fiscalization__ApiKey`, etc.). Nunca en
`appsettings.json`.

Config de negocio (en base, no en env vars): `Company.AutomaticInvoicing` + override opcional
`Branch.AutomaticInvoicing` (nullable). Resolución: **`sucursal ?? empresa`**. Default OFF.

---

## 10. Alcance decidido

- Facturación **opt-in por venta** (`requestInvoicing`), **todo-o-nada**. Sin facturación parcial.
- Aplica a ventas normales y de **Cuenta Corriente** (las dos son ventas). El **cobro de CC es un
  recibo**, no una factura: se factura el hecho venta, no el cobro.
- **Compras/proveedores no emiten.**
- Venta **sin cliente** → `receiver` null → el servicio resuelve Consumidor Final / Factura B sin
  identificar (`DocTipo 99`). Es el caso más común.
- **IVA**: `Sale` ya guarda `TotalAmount` (con IVA), `VatRate` y `VatAmount`; facturar es solo
  mapeo (`neto = total − iva`). Una sola alícuota por venta.

### Re-facturación — modelo listo, operación NO expuesta

El modelo soporta anular una factura y emitir otra por la misma venta (secuencias, `Voided`,
`CanInvoice`). **Pero no hay operación que lo dispare**: hoy la NC solo se emite al **anular la
venta**, y una venta anulada no se factura.

O sea: **modelo A, comportamiento B**. Es deliberado — mantiene la simplicidad de "una venta, una
factura" sin pintarnos en un rincón. El día que haga falta (caso típico: se emitió Factura B y el
cliente después trae el CUIT para que sea A, con la venta ya cobrada), exponerlo es **un command y
un botón**: sin migración, sin refactor de dominio.

---

## 11. Puntos abiertos

- **Recargo por tarjeta**: `CardSurchargeTotal` **no** está en `TotalAmount` (existe
  `EffectiveTotal`). Hoy se factura sobre `TotalAmount`. **Definir con el contador**, no es
  decisión del desarrollo.
- **Ventas sin `VatRate`** se facturan asumiendo **21%** contenido en el total (emisor
  Responsable Inscripto). Revisar antes de facturar histórico con otra alícuota real.
- **Alícuota única por venta**: no hay IVA mixto en un comprobante. Válido para ARCA hoy.

---

## Archivos

| Qué | Dónde |
|---|---|
| Entidad y reglas | `eiti.Domain/Sales/SaleFiscalDocument*.cs`, `SaleInvoicingView.cs` |
| Transición de anulación | `eiti.Application/Features/Sales/Common/SaleFiscalDocumentEffects.cs` |
| Mapeo venta → comprobante | `eiti.Application/Features/Sales/Common/SaleInvoicingService.cs` |
| Puerto y cliente HTTP | `Abstractions/Services/IFiscalizationService.cs`, `eiti.Infrastructure/Services/FiscalizationService.cs` |
| Firma del callback | `eiti.Infrastructure/Services/FiscalCallbackSignatureValidator.cs` |
| Endpoints | `eiti.Api/Controllers/FiscalController.cs`, `SalesController.cs` |
