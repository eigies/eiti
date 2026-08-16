# Teléfono de contacto en la venta (sin cliente)

## Objetivo
Permitir cargar un teléfono de contacto en la venta (paso 3, al lado de Dirección de entrega),
que aparezca en la fila de gestión debajo del nombre (donde hoy va customerPhone/referencia),
y que el filtro "por teléfono" busque también este campo además de customer.phoneNumber.
Espejar el patrón existente de `DeliveryAddress` para no duplicar lógica. Nueva feature (no existía).

## Backend
- [ ] Sale.cs: propiedad ContactPhone; param en constructor privado, Create y Update; SetContactPhone()
- [ ] SaleConfiguration: Property(ContactPhone).HasMaxLength(30)
- [ ] Migración AddSaleContactPhone
- [ ] CreateSaleCommand + Handler: pasar ContactPhone
- [ ] UpdateSaleCommand + Handler: pasar ContactPhone
- [ ] ListSalesItemResponse + Handler: devolver ContactPhone
- [ ] GetSaleById response + Handler: devolver ContactPhone (prefill edición)
- [ ] Validator create/update: MaximumLength(30)

## Frontend (sales-page — venta normal)
- [ ] sale.models.ts: contactPhone en SaleResponse, CreateSaleRequest, UpdateSaleRequest
- [ ] lineForm + editMetaForm: control contactPhone; reset
- [ ] Input teléfono en paso 3 (create) y en edición
- [ ] build request create/update: incluir contactPhone
- [ ] Fila gestión: mostrar sale.customerPhone || sale.contactPhone || documento || refs
- [ ] Filtro "por teléfono" (client-side): matchear customerPhone OR contactPhone
- [ ] Prefill editMetaForm.contactPhone desde sale.contactPhone al editar

## Verificación
- [ ] dotnet build eiti.Application (con deps) + migración
- [ ] ng build dev + prod
- [ ] Specs verdes

## Review
- Backend: ContactPhone en Sale (ctor/Create/Update/SetContactPhone), EF config (maxlen 30), migración
  AddSaleContactPhone (solo AddColumn), CreateSale/UpdateSale command+handler+validator, ListSales response+handler.
  GetSaleById NO lo necesita (el prefill de edición sale de la lista). Controllers sin cambios ([FromBody] directo).
- Frontend: contactPhone en CreateSaleRequest/SaleResponse; lineForm+editMetaForm control; input en paso 3 y
  edición; buildRequest lo incluye; update rápido lo pasa; fila muestra customerPhone||contactPhone||...;
  filtro "por teléfono" matchea ambos; prefill de edición desde sale.contactPhone.
- Build backend (Application con deps) OK; migración generada; ng build dev OK. Specs no afectados.
- Requiere deploy de BACKEND (Railway, Database.Migrate agrega la columna) + FRONT (Vercel).
- PENDIENTE: QA visual del usuario.

---

# Rediseño del dashboard — continuación 2026-08-15

- [x] Baseline: backend 225 tests; frontend build development.
- [x] Task 5: impedir que los importes viajen sin permiso financiero.
- [x] Task 6: publicar `GET /api/dashboard/summary` y verificar backend.
- [x] Tasks 7-8: modelos, servicio HTTP y preferencias del frontend.
- [x] Task 9: reescritura visual y funcional del dashboard.
- [x] Task 10 local: suite, build y revisión final; sin deploy.

## Review

- Backend: 234/234 tests; `eiti.Infrastructure` compila con 0 warnings y 0 errores.
- Frontend: 200/200 specs; build development correcto.
- Dashboard revisado con datos representativos en dark/light y breakpoints desktop/tablet/móvil.
- Review independiente resuelto: fechas calendario sin corrimiento UTC; detalle propio del dashboard
  con canceladas y controles de sucursal/importes; filtro de estado sobre hoy; restauracion de
  `canViewAllBranches`; reloj inyectable en tests; respuestas fuera de orden ignoradas; detalle
  limitado en servidor a un dia dentro de la ventana visible de los ultimos siete dias.
- Sin commit, merge, tag, push ni deploy, por pedido del usuario.
- Pendiente externo: Railway CLI no está autenticado, por lo que no se pudo ejecutar el SELECT temporal contra PostgreSQL real.
