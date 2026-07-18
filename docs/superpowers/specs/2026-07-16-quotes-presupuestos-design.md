# Presupuestos (Quotes) — Design Spec

## Contexto

Hoy el flujo de ventas (`eiti.Domain/Sales/Sale.cs`) soporta ventas normales (`Sale.Create`) y ventas de cuenta corriente (`Sale.CreateCc`), con `SaleStatus` (`OnHold`, `Paid`, `Cancel`), pagos (`SalePayment`), pagos CC (`SaleCcPayment`) y transporte. No existe ningún concepto de "presupuesto" — se verificó por grep (`quote|presupuesto|draft|estimate`) en todo el código, sin resultados.

Necesidad: el vendedor arma una cotización ("esto te saldría") para un cliente — que puede ser un cliente ya cargado o un prospecto sin cuenta todavía — y se la entrega en PDF. No impacta stock ni cuenta corriente. Si el cliente acepta, esa cotización se concreta en una venta CC real.

## Decisiones ya tomadas (confirmadas con el usuario)

- El presupuesto **no afecta stock** mientras está pendiente — es solo informativo, precios/cantidades de referencia.
- Es una **entidad nueva `Quote`**, separada de `Sale` — no se reutiliza `SaleStatus` para no mezclar presupuestos con ventas reales en reportes, rankings y queries existentes de `Sale`.
- El **cliente es opcional**: puede vincularse a un `Customer` ya cargado, o cargarse como prospecto con nombre/contacto en texto libre si todavía no está en el sistema. Al convertir a venta CC, si era prospecto, el flujo exige resolver un `CustomerId` real antes de confirmar.
- Tiene **fecha de vencimiento** (`ExpiresAt`, default configurable, ej. +7 días). Vencido el plazo, no se puede convertir directamente sin re-cotizar.
- Se entrega como **PDF descargable/imprimible**, generado client-side (mismo mecanismo jsPDF que ya usa el remito de venta).
- Al **convertir a venta CC**, los items/cliente/descuento son **editables** antes de confirmar — se pre-carga el formulario existente de `sales-cc` con los datos del presupuesto, pero el vendedor puede ajustarlos.

## Alcance

**Incluye:**
- Crear presupuesto: cliente (existente o prospecto), items con precio/cantidad, descuento general, vencimiento.
- Listar/filtrar presupuestos por estado, cliente, fecha.
- Ver detalle de un presupuesto y descargar su PDF.
- Cancelar un presupuesto pendiente.
- Convertir un presupuesto pendiente (no vencido) en una venta CC real, con posibilidad de editar antes de confirmar.
- Permisos nuevos: `quotes.access`, `quotes.create`, `quotes.convert`.

**Fuera de alcance (explícitamente):**
- Reserva/afectación de stock mientras el presupuesto está pendiente.
- Vencimiento automático por job/cron — se calcula al intentar convertir, no hay tarea en background.
- Conversión a ventas que no sean CC (venta de contado directa desde un presupuesto no está contemplada en esta primera versión).
- Envío automático por WhatsApp/email del presupuesto (solo PDF descargable/imprimible por ahora).
- Edición de un presupuesto ya `Converted`, `Expired` o `Cancelled`.

## Modelo de dominio

### `Quote` (nuevo aggregate root, `eiti.Domain/Quotes/`)

Análogo a `Sale` pero con su propio ciclo de vida, mucho más simple (sin pagos, sin transporte, sin stock).

Propiedades:
- `QuoteId` (VO), `CompanyId`, `BranchId`
- `CustomerId?` — cliente ya cargado
- `ProspectName?`, `ProspectContact?` — texto libre cuando no hay cliente cargado. Invariante: exactamente uno de (`CustomerId`) o (`ProspectName`) debe estar presente; se valida en el constructor de dominio (`ArgumentException` si ambos o ninguno).
- `Details` (colección privada `List<QuoteDetail>`, expuesta como `IReadOnlyCollection<QuoteDetail>`) — cada línea: `ProductId`, `Quantity`, `UnitPrice` (congelado al crear, no se recalcula si el precio de catálogo cambia después).
- `GeneralDiscountPercent`
- `TotalAmount` (calculado a partir de `Details` y el descuento)
- `ExpiresAt`
- `Status` (enum `QuoteStatus`: `Pending = 1`, `Converted = 2`, `Cancelled = 3`) — nótese que **no** hay un valor `Expired` persistido: el vencimiento es un estado derivado (`Status == Pending && ExpiresAt < now`), evaluado en el momento de consultar/convertir. Evita necesidad de un job que recorra y actualice filas.
- `ConvertedSaleId?` — referencia a la `SaleId` resultante
- `Code` (numeración propia, ej. `PRES-0001`, secuencia independiente del `Code` de `Sale`)
- `CreatedAt`, `CreatedByUserId`

Métodos de dominio:
- `Create(...)` — factory, valida invariante cliente/prospecto y que `Details` no esté vacío.
- `Cancel()` — solo si `Status == Pending`; si no, error de dominio.
- `MarkConverted(SaleId saleId)` — solo si `Status == Pending` y no vencido; setea `Status = Converted`, `ConvertedSaleId = saleId`.
- Es un aggregate independiente: no expone setters públicos sobre `Details`, mismo patrón que `Sale`.

### `QuoteDetail` (entidad hija, dentro del mismo archivo o `QuoteDetail.cs`)

`ProductId`, `Quantity`, `UnitPrice`, `LineTotal` (calculado).

## Backend — Features (Vertical Slice)

Nuevo directorio `eiti.Application/Features/Quotes/`, siguiendo el patrón ya establecido (uno por handler, `Errors.cs` separado):

- **`Commands/CreateQuote`** (`CreateQuoteCommand`, `CreateQuoteHandler`, `CreateQuoteValidator`, `CreateQuoteErrors`, `CreateQuoteResponse`) — recibe `BranchId`, `CustomerId?`, `ProspectName?`, `ProspectContact?`, `Details[]`, `GeneralDiscountPercent`, `ExpiresAt`. Requiere permiso `quotes.create`. Auth check con `EnsureAuthenticated()` al inicio del handler (patrón estándar del proyecto).
- **`Commands/CancelQuote`** (`CancelQuoteCommand`, `CancelQuoteHandler`, `CancelQuoteErrors`) — recibe `QuoteId`. Requiere `quotes.access`.
- **`Commands/ConvertQuoteToSale`** (`ConvertQuoteToSaleCommand`, `ConvertQuoteToSaleHandler`, `ConvertQuoteToSaleErrors`) — recibe `QuoteId` + los mismos campos que `CreateCcSaleCommand` (`BranchId`, `CustomerId` — **obligatorio en este paso**, `Details[]`, `TradeIns?`, `GeneralDiscountPercent`, `ManualOverridePrice?`), ya editados por el usuario en el front. El handler:
  1. Carga el `Quote`, valida `Status == Pending` y `ExpiresAt >= now` (si no, `ConvertQuoteToSaleErrors.Expired` o `.AlreadyConverted`/`.Cancelled`).
  2. Despacha internamente un `CreateCcSaleCommand` vía `IMediator.Send(...)` — reutiliza el handler existente en vez de duplicar la lógica de creación de venta CC.
  3. Si la creación de la venta fue exitosa, llama `quote.MarkConverted(saleId)` y persiste.
  4. Devuelve la respuesta de la venta creada (mismo shape que `CreateCcSaleHandler`).
  Requiere permiso `quotes.convert`.
- **`Queries/ListQuotes`** (`ListQuotesQuery`, `ListQuotesHandler`, `QuoteListItemResponse`) — filtros: `Status?`, `DateFrom?`, `DateTo?`, `CustomerId?`. Mismo patrón de filtrado de sucursal post-query que `ListSalesHandler` (según `CanViewAllBranches`/`AllowedBranchIds`). El campo `Status` en la respuesta de listado se calcula como "Vencido" en el mapper si `Status == Pending && ExpiresAt < now`, sin tocar el valor persistido. Requiere `quotes.access`.
- **`Queries/GetQuoteById`** (`GetQuoteByIdQuery`, `GetQuoteByIdHandler`, `QuoteDetailResponse`) — para el detalle y el PDF. Mismo cálculo de "Vencido" derivado. Requiere `quotes.access`.

### Infraestructura

- `IQuoteRepository` (`eiti.Application/Abstractions/Repositories/`) — `GetByIdAsync`, `AddAsync`, `ListAsync` (con filtros), acorde al patrón de `ISaleRepository` (solo lo que los handlers necesitan, sin `GetAll()` genérico).
- `QuoteRepository` en `eiti.Infrastructure/Persistence/Repositories/`.
- `QuoteConfiguration : IEntityTypeConfiguration<Quote>` + `QuoteDetailConfiguration` en `eiti.Infrastructure/Persistence/Configurations/` — `decimal(18,2)` para montos, `HasIndex` en `CustomerId`, `BranchId`, `CompanyId`. Navigation de `Details` con `UsePropertyAccessMode(PropertyAccessMode.Field)` (mismo patrón que `Sale.Details`).
- Nuevo `DbSet<Quote>` en `ApplicationDbContext`.
- Migración EF nueva (`AddQuotes`).

### Permisos

En `PermissionCodes.cs`: `quotes.access`, `quotes.create`, `quotes.convert`. Recordar los 3 lugares (lección ya documentada en el proyecto): agregar también a `PermissionCatalog.All` (allowlist de validación de access profiles) y asignar en `RoleCatalog.cs` a los roles correspondientes (Owner/Admin/Vendedor). Reiniciar la API si se prueba en caliente, porque `PermissionCatalog.All` es estático.

### Controller

`QuotesController` en `eiti.Api/Controllers/`, mismo patrón que `SalesController`: endpoints `POST /api/quotes`, `GET /api/quotes`, `GET /api/quotes/{id}`, `POST /api/quotes/{id}/cancel`, `POST /api/quotes/{id}/convert`.

## Frontend (Angular)

Nuevo feature `src/app/features/quotes/`:

- **Modelos** `core/models/quote.models.ts`: `QuoteResponse`, `QuoteListItemResponse`, `QuoteDetailResponse`, `CreateQuoteRequest`, `ConvertQuoteRequest`, `QuoteStatus` enum (`Pending`, `Converted`, `Cancelled`, y el derivado visual `Expired` que llega calculado desde el backend en la respuesta, no como estado propio del enum de dominio).
- **Servicio** `core/services/quote.service.ts` (`providedIn: 'root'`): `listQuotes(filters)`, `getQuoteById(id)`, `createQuote(request)`, `cancelQuote(id)`, `convertQuote(id, request)`. URLs desde `environment.apiUrl`, sin hardcodear.
- **`quotes-list.component`** (standalone, `OnPush`) — listado con filtros de estado/cliente/fecha, reutilizando el patrón visual de `sale-list-item` de `features/sales/components/`. Chips: Pendiente / Convertido / Vencido / Cancelado (Vencido calculado en frontend o recibido del backend, no un estado propio).
- **`quote-form.component`** (Reactive Forms, `FormBuilder` + `Validators`) — selector de `Customer` existente (autocomplete, reusa `CustomerService`) **o** toggle "cliente sin cargar" que habilita inputs de texto libre (nombre/contacto). Reutiliza `product-picker-modal` compartido para cargar items. Campo `ExpiresAt` (date picker, default +7 días). Validación cruzada cliente/prospecto también en el frontend (mismo criterio que el dominio backend).
- **`quote-detail-modal.component`** — solo lectura + botón "Descargar PDF" + botón "Convertir a venta" (visible solo si `status === Pending` y no vencido).
- **Flujo de conversión**: el botón "Convertir a venta" pasa los datos del presupuesto como `initialData` al formulario ya existente de `sales-cc` (`features/sales/sales-cc/sales-cc.component.ts`), pre-cargando cliente/items/descuento. Si el `Quote` no tenía `CustomerId` (era prospecto), el formulario obliga a seleccionar/crear un `Customer` antes de habilitar "Confirmar". Al confirmar, se llama `QuoteService.convertQuote(id, request)` (no `SaleService.createCcSale` directo), para mantener el vínculo `ConvertedSaleId` en el backend.
- **PDF**: nuevo `quote-pdf.util.ts` en `shared/` usando jsPDF (mismo mecanismo que el remito de venta). Incluye datos de empresa, cliente/prospecto, tabla de items, total, "Válido hasta DD/MM/AAAA" destacado, y aclaración "Presupuesto — no constituye una venta". Se genera 100% client-side a partir de `GetQuoteById`, sin endpoint extra.
- **Ruta**: `/quotes`, lazy-loaded vía `loadComponent`, con `data: { permission: PermissionCodes.QuotesAccess }`. Link en el navbar cerca de "Ventas", guardado por el mismo permiso.
- **Permisos frontend**: agregar `quotesAccess`, `quotesCreate`, `quotesConvert` a `permission.models.ts` (map `PermissionCodes` + array `PermissionCatalog`), espejando los códigos del backend.

## Testing

- Tests unitarios de dominio (`eiti.Tests`) para `Quote`: invariante cliente/prospecto (ambos o ninguno → excepción), `Cancel()` solo desde `Pending`, `MarkConverted()` rechaza si ya convertido/cancelado/vencido.
- Test de `ConvertQuoteToSaleHandler`: caso feliz (crea `Sale` CC + marca `Quote` convertido), caso vencido (rechaza sin tocar el `Quote`), caso ya convertido (rechaza).
- Verificación manual frontend: crear presupuesto con prospecto → descargar PDF → convertir (forzando alta de cliente) → confirmar que la venta CC resultante aparece en `ListCcSales` y el presupuesto queda `Converted` con el link a esa venta.

## Riesgos / notas

- `UnitPrice` en `QuoteDetail` se congela al crear el presupuesto — si el precio de catálogo cambió para cuando se convierte, el formulario de conversión mostrará el precio viejo (editable) y no lo recalcula automáticamente. Es intencional: el vendedor decide si actualiza o no.
- El estado "Vencido" es puramente derivado (no persistido) para mantener el modelo simple — cualquier vista/reporte que necesite filtrar por vencidos debe calcularlo con `ExpiresAt < now`, no confiar en `Status`.
