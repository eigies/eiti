## Agent-F3-Fix - 2026-03-17
### Reimplementación correcta del desglose de pagos por medio de pago

**Problema anterior:** el breakdown estaba en `GetCashSessionSummary` (sesión abierta), no en el historial.

**Diseño correcto:** el breakdown va en cada sesión del historial.

### Cambios realizados
- `CashSessionSummaryResponse.cs`: eliminado `PaymentBreakdown` — la sesión abierta no necesita breakdown
- `GetCashSessionSummaryHandler.cs`: eliminada inyección de `ISaleRepository` y toda la lógica de saleIds/payments — handler volvió simple
- `CashSessionMapper.MapSummary()`: eliminado parámetro `payments` y lógica de breakdown — vuelve a ser simple
- `CashSessionResponse.cs`: agregado `IReadOnlyList<PaymentMethodBreakdownItem> PaymentBreakdown` — el historial sí lo necesita
- `CashSessionMapper.Map()`: acepta `IReadOnlyList<SalePayment>? payments = null` opcional; llama a `BuildBreakdown()` extraído como método privado
- `SaleRepository.GetPaymentsBySaleIdsAsync`: corregido bug de value converter usando `EF.Property<Guid>(payment, "SaleId")` en lugar de `payment.SaleId.Value`
- `ListCashSessionHistoryHandler`: inyecta `ISaleRepository`; hace una sola query para todos los sale IDs de todas las sesiones juntas; distribuye pagos por sesión con un Dictionary
- Frontend `cash.models.ts`: `CashSessionResponse` incluye `paymentBreakdown`; `CashSessionSummaryResponse` ya no la incluye
- Frontend `cash.component.ts`: eliminado bloque `payment-breakdown` del `currentSummary`; agregado `session-breakdown` inline debajo de cada entrada del historial; `toSummary()` ya no incluye `paymentBreakdown`; eliminada actualización estale de `currentSummary.paymentBreakdown` en `loadSalePaymentMethods`

### Lecciones aprendidas
- **Diseño de feature**: ubicar el breakdown donde tiene sentido de negocio (historial de sesiones cerradas), no donde es fácil de implementar (sesión abierta/summary)
- **EF Core value objects y Contains**: `payment.SaleId.Value` no siempre se traduce a SQL con value converters — usar `EF.Property<Guid>(payment, "SaleId")` es el approach correcto para evitar client-side evaluation
- **Eficiencia N+1**: cuando hay múltiples sesiones, hacer una sola query con todos los IDs y luego distribuir in-memory con un Dictionary es siempre más eficiente que N queries
- **Frontend breakdown en historial**: el breakdown viene del backend como parte del `CashSessionResponse` — no se necesita `computePaymentBreakdown()` para el historial (aunque sigue siendo útil para exports)
- **Revert correcto**: al revertir un feature, leer TODOS los archivos afectados antes de editar para no dejar firma desincronizada

---

## Agent-F3 - 2026-03-17
### Cambios realizados
- Creado `PaymentMethodBreakdownItem.cs` (record) en `eiti.Application/Features/CashSessions/Common/`
- Enriquecido `CashSessionSummaryResponse` con `IReadOnlyList<PaymentMethodBreakdownItem> PaymentBreakdown`
- Agregado `GetPaymentsBySaleIdsAsync(IEnumerable<Guid> saleIds)` a `ISaleRepository` e implementado en `SaleRepository` con query directa a `SalePayments` via `payment.SaleId.Value` (evita el problema de Contains con value objects)
- Actualizado `GetCashSessionSummaryHandler` para inyectar `ISaleRepository`, extraer sale IDs de los movimientos `SaleIncome`, y pasar los pagos al mapper
- Actualizado `CashSessionMapper.MapSummary` para aceptar `IReadOnlyList<SalePayment>`, agrupar por `SalePaymentMethod`, y construir el breakdown
- Frontend: `PaymentMethodBreakdownItem` agregado a `cash.models.ts`; `CashSessionSummaryResponse` incluye `paymentBreakdown`
- Frontend: `cash.component.ts` — `currentSummary` se actualiza desde backend tras cargar sesion (llamada a `getSummary()`)
- Frontend: Footer visual "Desglose por medio de pago" reemplaza el bloque anterior con cards individuales por método
- Frontend: `exportSession()` incluye hoja "Medios de Pago" en el XLSX
- Frontend: `exportSessionsPdf()` incluye sección de desglose al pie de cada sesión en el PDF

### Lecciones aprendidas
- EF Core value objects con `Contains`: usar `.SaleId.Value` (tipo Guid primitivo) en lugar de `.SaleId` (tipo SaleId record) para que EF pueda traducir a SQL correctamente
- Cuando el linter de Angular reformatea el archivo inmediatamente, las edits fallaban por "file modified". La solución es leer siempre antes de cada edit y usar strings únicos y precisos en old_string
- Para el `currentSummary` con breakdown: mejor llamar al endpoint `/summary` del backend en vez de computar localmente, ya que el backend tiene acceso directo a la DB
- Para exports (PDF/Excel) de historial: computar el breakdown localmente desde el mapa `salesBySaleId` ya cargado, evitando N llamadas extra al backend

---

## Agent-F1 - 2026-03-17
### Cambios realizados
- Creado `SaleSourceChannel.cs` enum en `eiti.Domain/Sales/` con 6 valores: Referral, WhatsApp, Facebook, Web, Instagram, Other
- Agregado `SourceChannel? SourceChannel` y `SetSourceChannel()` en `Sale.cs`
- Actualizado `SaleConfiguration.cs` para mapear la columna nullable int `SourceChannel` usando `HasConversion<int?>()`
- Actualizado `CreateSaleCommand` y `UpdateSaleCommand` con el campo `SaleSourceChannel? SourceChannel = null` (opcional con default null)
- Llamada `sale.SetSourceChannel(request.SourceChannel)` agregada en `CreateSaleHandler` y `UpdateSaleHandler` antes de SaveChanges
- Actualizado `ListSalesItemResponse` para incluir `SaleSourceChannel? SourceChannel`
- Actualizado `ListSalesHandler` mapper para pasar `sale.SourceChannel`
- Migration `AddSaleSourceChannel` generada via EF Core (incluye también `TransferCounterpartSessionId` del F2 pendiente)
- Fixed pre-existing error en `CashSessionMapper.MapSummary` que Agent F2 dejó incompleto (faltaba el argumento `PaymentBreakdown` en el constructor)
- Frontend: agregados `SaleSourceChannel`, `SALE_SOURCE_CHANNELS`, `saleSourceChannelLabel` en `sale.models.ts`
- Frontend: `sourceChannel` agregado a `lineForm`, `editMetaForm`, `filterForm` con `[ngValue]="null"` como opción vacía
- Frontend: filtrado client-side por canal en `loadSales()` (el endpoint no acepta ese param aún)
- Frontend: badge `chip--channel` (violeta) en sale-card chips con `saleChannelLabel()` helper
- Frontend: select de canal en formulario de nueva venta, edición y filtro de lista
- Frontend build: 0 errores, build time ~7s

### Lecciones aprendidas
- El linter/editor puede modificar archivos automáticamente (fue el caso con `CashSessionMapper.cs`) — revisarlo antes de compilar para no generar conflictos de firma
- La migration de EF Core captura todos los cambios pendientes del modelo, incluyendo los de otros agentes que no ejecutaron migrate — importante verificar qué incluye la migration antes de aplicarla
- Para enums opcionales en un record de C#, usar `SaleSourceChannel? SourceChannel = null` al final de los parámetros posicionales para compatibilidad con llamadas existentes
- En Angular, `[ngValue]="null"` es necesario para que el select envíe null (no la string "null") cuando se selecciona la opción vacía
- El filtrado por canal se hace client-side porque el backend API no recibe ese param — esto limita eficiencia si hay muchas ventas, pero es correcto para esta iteración

## Agent-F2 - 2026-03-17
### Cambios realizados
- Extendido `CashMovementType` con `CashTransferOut = 5` y `CashTransferIn = 6`
- Agregado `TransferCounterpartSessionId` (Guid nullable) en `CashMovement` como campo de dominio (constructor + Create factory con parámetro opcional)
- Agregado `RegisterTransferOut()` y `RegisterTransferIn()` en `CashSession`, siguiendo el patrón de `RegisterWithdrawal()`; TransferOut valida balance mínimo no negativo
- Creado `CashTransferErrors.cs` con 5 errores tipados
- Creado `CreateCashTransferCommand` (retorna `Result` sin genérico), `CreateCashTransferCommandValidator` y `CreateCashTransferHandler`
- Endpoint `POST /api/cashsessions/transfers` en `CashSessionsController`
- `CashMovementConfiguration` actualizado con `.IsRequired(false)` para `TransferCounterpartSessionId`
- Migration `AddCashTransferSupport` generada correctamente
- Fixed pre-existing bug: `CashSessionMapper.MapSummary` ya tenía firma de 2 args (fue corregida por otro agente antes del build)
- Frontend: `CashService.transfer()` agrega método HTTP POST
- Frontend: `transferForm` (ReactiveForm), `showTransferModal`, `openTransferModal()`, `closeTransferModal()`, `submitTransfer()`, getter `otherDrawers`
- Frontend: botón "↔ Transferir" aparece junto a "Registrar extraccion" cuando existen otras cajas disponibles
- Frontend: modal overlay con select de caja destino, monto y descripcion; sigue el mismo patrón de modal existente en el app
- Frontend: badge styles para `CashTransferOut` (indigo) y `CashTransferIn` (cyan) en historial de movimientos
- Frontend: `translateType()` incluye traducciones para los nuevos tipos de movimiento

### Lecciones aprendidas
- Para features de transferencia entre entidades, el handler debe buscar ambas sesiones por CashDrawerId (no por SessionId) usando `GetOpenByDrawerAsync` — así el cliente solo necesita conocer los IDs de las cajas, no de las sesiones
- El campo `TransferCounterpartSessionId` en CashMovement se guarda en ambos lados (Out y In) para trazabilidad bidireccional sin relación de FK explícita
- Al agregar un botón extra en un form con grid de N columnas fijas, es más limpio envolver el form en un flex container con el botón externo (`withdraw-row`) que forzar una nueva columna en el grid
- El enum `CashMovementType` usa valores enteros explícitos (=1,2,3...) — siempre continuar la secuencia numérica explícitamente al agregar nuevos valores
- Los `data-type` attributes en badges del HTML se enlazan a CSS con `[attr.data-type]="movement.typeName"` — agregar nuevas reglas CSS con el typeName exacto del backend

## Agent-F5 - 2026-03-17
### Cambios realizados
- Agregado `compactMode: boolean = true` y getter `showHiddenColumns` en products.component.ts
- Columnas Descripcion, Unitario, Reservado, Actualizado ocultas por defecto en modo compacto; visibles cuando `!compactMode || bulkEditMode`
- Botón "Columnas ▼ / Compacto ▲" en bulk-bar para toggle de densidad (visible solo cuando no está en bulkEditMode)
- Extendido `ProductModalMode` con 'detail'; agregado `openDetailModal()`, `toggleCompactMode()`, guard en `submitModal()`
- Botón ojo (&#128065;) en columna Acciones con hover state amber usando clase `.btn-icon--eye`
- Detail panel en modal con 4 secciones (Identidad, Precios, Stock global, Registro); respeta `canViewCostPrice`; botón "Editar" que transiciona a modo edit
- CSS: padding th reducido 0.72→0.56rem, td 0.82→0.6rem, min-width tabla 1320→900px
- Añadidos estilos `.btn-icon`, `.btn-icon--eye`, `.detail-panel`, `.detail-section`, `.detail-row`, `.detail-row__val--*`

### Lecciones aprendidas
- Al agregar un nuevo modal mode, siempre: (1) actualizar el type union, (2) agregar guard en submitModal(), (3) excluir del bloque form existente con `modalMode !== 'nuevo'`, (4) actualizar el modal__head ternario
- `showHiddenColumns = !compactMode || bulkEditMode` es un patrón limpio para mostrar columnas extra durante edición masiva sin romper la visibilidad por defecto
- El getter de columnas visibles debe estar en el componente (no en el template) para mantener el template limpio y la lógica testeeable
- Al reducir padding de celdas, reducir también el min-width de la tabla para que con menos columnas no sobre espacio horizontal vacío
- El botón de detalle en la columna Acciones debe tener `*ngIf="!bulkEditMode"` igual que Editar y Eliminar para no confundir en modo bulk
