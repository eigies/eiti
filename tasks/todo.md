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
