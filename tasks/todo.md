# F3: Desglose de ingresos por medio de pago en caja (footer + PDF/Excel)

- [x] Crear PaymentMethodBreakdownItem.cs en CashSessions/Common/
- [x] Agregar PaymentBreakdown a CashSessionSummaryResponse
- [x] Agregar GetPaymentsBySaleIdsAsync a ISaleRepository
- [x] Implementar GetPaymentsBySaleIdsAsync en SaleRepository
- [x] Actualizar GetCashSessionSummaryHandler para cargar pagos por sale IDs
- [x] Actualizar CashSessionMapper.MapSummary para construir PaymentBreakdown
- [x] Build backend (0 errores)
- [x] Agregar PaymentMethodBreakdownItem a cash.models.ts (frontend)
- [x] Agregar paymentBreakdown a CashSessionSummaryResponse frontend
- [x] Actualizar cash.component.ts: footer visual de desglose
- [x] Actualizar exportSession() Excel: filas/hoja de desglose
- [x] Actualizar exportSessionsPdf(): sección de desglose al pie de cada sesion
- [x] Documentar en tasks/lessons.md

---

# Agent-F5: Mejoras UI grilla de productos (solo frontend)

## Plan
- [x] Leer Rules.md y archivos existentes (ts, html, css)
- [x] CAMBIO 1: Ocultar columnas no esenciales (Descripcion, Unitario, Reservado, Actualizado)
  - [x] Agregar `compactMode: boolean` y getter `showHiddenColumns` en .ts
  - [x] Agregar botón "Columnas ▼ / Compacto ▲" en bulk-bar
  - [x] Agregar `*ngIf="showHiddenColumns"` en th+td de columnas ocultables
  - [x] En bulkEditMode `showHiddenColumns` retorna true automáticamente
- [x] CAMBIO 2: Botón ojo y popup de detalle
  - [x] Extender `ProductModalMode` a incluir 'detail'
  - [x] Agregar `openDetailModal(product)` y `toggleCompactMode()` en .ts
  - [x] Agregar `submitModal()` guard para 'detail'
  - [x] Agregar botón ojo antes de "Editar" en columna Acciones
  - [x] Agregar bloque detail-panel en modal HTML
  - [x] Actualizar modal__head para título/icono del modo 'detail'
  - [x] Excluir 'detail' del form edit/delete
- [x] CAMBIO 3: Ajustes visuales de densidad en CSS
  - [x] Reducir padding th: 0.72→0.56rem; td: 0.82→0.6rem
  - [x] Reducir min-width de .tbl: 1320px→900px
  - [x] Agregar estilos .btn-icon, .btn-icon--eye, .detail-panel, .detail-section, .detail-row, etc.
- [x] Verificar bulkEditMode y modos edit/delete/stock (no se tocó lógica existente)
- [x] Documentar en tasks/lessons.md

---

# F2: Pase entre cajas (transferencia de dinero entre CashDrawers)

- [x] Actualizar enum CashMovementType (CashTransferOut=5, CashTransferIn=6)
- [x] Agregar TransferCounterpartSessionId a CashMovement
- [x] Agregar RegisterTransferOut / RegisterTransferIn a CashSession
- [x] Crear CashTransferErrors.cs
- [x] Crear CreateCashTransferCommand.cs
- [x] Crear CreateCashTransferCommandValidator.cs
- [x] Crear CreateCashTransferHandler.cs
- [x] Agregar endpoint POST /cash-sessions/transfers en CashSessionsController
- [x] Actualizar CashMovementConfiguration para TransferCounterpartSessionId
- [x] Crear migration AddCashTransferSupport
- [x] Build backend (0 errores)
- [x] Agregar transferencia al CashService (frontend)
- [x] Agregar modal de transferencia al cash.component.ts
- [x] Documentar en tasks/lessons.md

---

# F1: Canal de origen en ventas

- [x] Crear enum SaleSourceChannel en eiti.Domain/Sales/
- [x] Agregar propiedad y método SetSourceChannel en Sale.cs
- [x] Actualizar SaleConfiguration.cs para mapear la nueva columna
- [x] Crear migration AddSaleSourceChannel
- [x] Agregar SaleSourceChannel? SourceChannel a CreateSaleCommand
- [x] Agregar SaleSourceChannel? SourceChannel a UpdateSaleCommand
- [x] Llamar sale.SetSourceChannel en CreateSaleHandler
- [x] Llamar sale.SetSourceChannel en UpdateSaleHandler
- [x] Agregar SourceChannel a ListSalesItemResponse
- [x] Incluir SourceChannel en ListSalesHandler mapper
- [x] Build backend (0 errores)
- [x] Agregar sourceChannel a SaleResponse en sale.models.ts (frontend)
- [x] Agregar sourceChannel al CreateSaleRequest en sale.models.ts
- [x] Agregar sourceChannel al lineForm y editMetaForm en sales-page.component.ts
- [x] Agregar select de canal en el formulario de nueva venta (HTML)
- [x] Agregar select de canal en el formulario de edición (HTML)
- [x] Mostrar badge de canal en sale-card (HTML)
- [x] Agregar filtro por canal en la lista (HTML + TS)
- [x] Build frontend (0 errores)
- [x] Documentar en tasks/lessons.md
