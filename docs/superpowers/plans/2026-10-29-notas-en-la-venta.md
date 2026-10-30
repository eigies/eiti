# Notas en la venta — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Una observación de texto libre por venta (minorista y CC) que se carga al vender, se corrige después y sale impresa en la factura.

**Architecture:** `Sale.Notes` (dominio, ≤ 500, vacío = null) se fija con `Sale.SetNotes`. Alta minorista, alta CC y conversión de presupuesto la reciben en su comando (los controllers bindean el comando entero, así que llega por HTTP). La corrección posterior va por un comando propio `UpdateSaleNotes` (`PUT /api/sales/{id}/notes`), nunca por `UpdateSale`. La nota viaja en `GetSaleById`, `ListSales` y `SaleInvoicePrintResponse`; el front la muestra/edita con un componente compartido y la dibuja en el PDF de la factura.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core + Npgsql, xUnit · Angular 16 standalone, Jasmine/Karma.

**Spec:** `docs/superpowers/specs/2026-10-29-notas-recargo-descuento-design.md`, sección **1**.

## Global Constraints

- Back: worktree `C:/Eiti/eiti/.claude/worktrees/feature+notas-recargo-descuento`, rama `feature/notas-recargo-descuento`. Front: worktree `C:/EiTeFront/eiti-front/.claude/worktrees/notas-recargo-descuento`, misma rama.
- Largo máximo **500**; vacío o solo espacios = `null`; se guarda recortada (`Trim`).
- Editable en cualquier estado **salvo anulada**; con factura emitida también (la nota no es dato fiscal y no viaja al servicio fiscal).
- Permiso para editar después: `sales.update` (`PermissionCodes.SalesUpdate`).
- Precarga al convertir un presupuesto: `Presupuesto <código del presupuesto>` (editable).
- Errores de negocio con `Result.Failure` y constantes en `*Errors.cs`; validators con FluentValidation; handler arranca con `EnsureAuthenticated()`.
- Una sola migración EF (`AddSaleNotes`), generada desde `eiti.Infrastructure`. La API corriendo bloquea DLLs: frenarla antes de compilar el back.
- UI: etiqueta "Observaciones"; `<textarea>` con la clase de los textareas existentes de la app; nada de controles nativos nuevos fuera de ese patrón.
- PDF: bloque "Observaciones" debajo de los ajustes, solo si hay nota.
- Verificación: `dotnet build eiti.Application/eiti.Application.csproj` + `dotnet test eiti.Tests`; front `ng build --configuration development` + `ng test --watch=false --browsers=ChromeHeadless`.

## Review Focus

- Nota de 501 caracteres por API → 400 de validación, no 500. → test en Task 1 (validator).
- Nota con solo espacios → queda `null`, no un string vacío. → test en Task 1 (dominio).
- Editar la nota de una venta anulada → error de negocio claro. → test en Task 2.
- Editar la nota de una venta facturada → se permite y la factura reimpresa la muestra. → test en Task 2 (handler) y e2e.
- Venta sin nota → el PDF no dibuja un bloque "Observaciones" vacío. → test en Task 5.

---

### Task 1: Dominio, persistencia y alta (minorista, CC, presupuesto)

**Files:**
- Modify: `eiti.Domain/Sales/Sale.cs` (propiedad + `SetNotes`)
- Modify: `eiti.Infrastructure/Persistence/Configurations/SaleConfiguration.cs`
- Create: migración `AddSaleNotes` (desde `eiti.Infrastructure`)
- Modify: `CreateSaleCommand.cs`/`CreateSaleHandler.cs`/`CreateSaleValidator.cs`, `CreateCcSaleCommand.cs`/`CreateCcSaleHandler.cs`/validator, `ConvertQuoteToSaleCommand.cs`/`ConvertQuoteToSaleHandler.cs`
- Test: `eiti.Tests/SaleNotesTests.cs`

**Interfaces:**
- Produces: `Sale.Notes : string?`; `Sale.SetNotes(string? notes)` (trim, vacío → null, > 500 → `ArgumentException`); `public const int NotesMaxLength = 500` en `Sale`; `string? Notes = null` al final de `CreateSaleCommand`, `CreateCcSaleCommand` y `ConvertQuoteToSaleCommand`.

- [ ] Tests (fallan primero): `SetNotes` recorta, `"   "` → `null`, 501 caracteres lanza; validator de `CreateSaleCommand` y de `CreateCcSaleCommand` rechazan 501 con error en `Notes`; `ConvertQuoteToSaleHandler` reenvía `Notes` al `CreateCcSaleCommand` (spy de `ISender`, patrón de `ConvertQuoteToSaleHandlerTests`).
- [ ] Implementar; handlers llaman `sale.SetNotes(request.Notes)` junto a `SetContactPhone`. EF: `HasMaxLength(Sale.NotesMaxLength)`, opcional. Migración.
- [ ] `dotnet test eiti.Tests --filter "FullyQualifiedName~SaleNotes|FullyQualifiedName~ConvertQuoteToSale"` verde → commit `feat(ventas): observaciones en la venta`.

### Task 2: Corregir la nota después (`UpdateSaleNotes`)

**Files:**
- Create: `eiti.Application/Features/Sales/Commands/UpdateSaleNotes/{UpdateSaleNotesCommand,UpdateSaleNotesHandler,UpdateSaleNotesValidator,UpdateSaleNotesErrors}.cs`
- Modify: `eiti.Api/Controllers/SalesController.cs` (`PUT {id}/notes`, body `UpdateSaleNotesRequest(string? Notes)` en `eiti.Api` junto a los otros requests del controller)
- Test: `eiti.Tests/SaleNotesTests.cs`

**Interfaces:**
- Produces: `UpdateSaleNotesCommand(Guid SaleId, string? Notes) : IRequest<Result>, IRequirePermissions` (`SalesUpdate`); errores `Sales.UpdateNotes.NotFound` (NotFound) y `Sales.UpdateNotes.Cancelled` (Conflict, "No se pueden editar las observaciones de una venta anulada.").

- [ ] Tests: venta inexistente → NotFound; venta anulada → Cancelled; venta pagada y facturada → OK y `sale.Notes` actualizado; validator rechaza 501.
- [ ] Implementar (repositorio de ventas existente, `SaveChangesAsync` al final) + endpoint.
- [ ] Tests verdes → commit `feat(ventas): corregir las observaciones de una venta`.

### Task 3: La nota sale en las lecturas y en el print

**Files:**
- Modify: `GetSaleByIdResponse.cs`/`GetSaleByIdHandler.cs`, `ListSalesItemResponse.cs`/`ListSalesHandler.cs`, `SaleInvoicePrintResponse.cs`/`GetSaleInvoicePrintHandler.cs`
- Test: `eiti.Tests/GetSaleInvoicePrintHandlerTests.cs` (+ el test que arme `GetSaleById` si existe)

**Interfaces:**
- Produces: `string? Notes` en `GetSaleByIdResponse`, `ListSalesItemResponse` y `SaleInvoicePrintResponse` (último parámetro de cada record).

- [ ] Test: el print de una venta con nota trae `Notes`; sin nota, `null`.
- [ ] Implementar → `dotnet build eiti.Application/eiti.Application.csproj` + `dotnet test eiti.Tests` completos verdes → commit `feat(ventas): las observaciones viajan al detalle, al listado y al print`.

### Task 4: Front — modelos, servicio y componente compartido de notas

**Files:**
- Modify: `src/app/core/models/sale.models.ts` (`notes?: string | null` en `CreateSaleRequest`, `CreateCcSaleRequest`, `SaleResponse`, `SaleByIdResponse`, `SaleInvoicePrintResponse`), el request de conversión en `quote.models.ts`/`quote.service.ts`
- Modify: `src/app/core/services/sale.service.ts` (`updateSaleNotes(id: string, notes: string | null): Observable<void>` → `PUT ${base}/${id}/notes`)
- Create: `src/app/shared/components/sale-notes/sale-notes.component.{ts,html,css,spec.ts}`

**Interfaces:**
- Produces: `<app-sale-notes [saleId]="id" [notes]="notes" [canEdit]="bool" (saved)="onNotesSaved($event)">` — muestra la nota (o "Sin observaciones"), botón "Editar" si `canEdit`, textarea con contador `n/500`, "Guardar"/"Cancelar"; al guardar llama `updateSaleNotes` y emite `saved: string | null`; error → `toast.error(detail ?? 'No se pudieron guardar las observaciones.')`.

- [ ] Spec del componente: guarda recortado y emite; vacío emite `null`; error muestra toast y no emite; sin `canEdit` no hay botón Editar.
- [ ] Implementar → spec verde → commit.

### Task 5: Front — formularios, detalle y PDF

**Files:**
- Modify: `sales-page.component.{ts,html}` (control `notes` en el alta; `buildRequest` lo manda; `<app-sale-notes>` en la tarjeta expandida, `canEdit` = `sales.update` y venta no anulada)
- Modify: `sales-cc/sales-cc.component.{ts,html}` (campo Observaciones; con presupuesto, arranca en `Presupuesto <código>`; se manda en alta y en conversión)
- Modify: `quotes.component.ts` (`quotePrefill.code`)
- Modify: `clients/customer-account.component.html` (`<app-sale-notes>` en el modal de detalle)
- Modify: `shared/services/invoice-pdf.service.ts` (bloque "Observaciones")
- Test: specs existentes de cada componente + `invoice-pdf.service` (si no hay spec, crear uno mínimo para el bloque)

- [ ] Tests: `buildRequest` incluye `notes`; la conversión arranca con `Presupuesto PRE-…`; el PDF dibuja "Observaciones" solo con nota.
- [ ] Implementar → `ng build --configuration development` + suite completa verde → commit + push.

### Task 6: E2E local (homologación)

- [ ] Venta minorista con nota y factura → PDF con "Observaciones"; venta CC con nota → corrección desde el detalle de la CC → reimpresión con la nota nueva; presupuesto convertido → nota precargada; venta anulada → sin "Editar".
