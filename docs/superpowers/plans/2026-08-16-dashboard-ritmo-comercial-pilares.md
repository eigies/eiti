# Dashboard Commercial Rhythm Pillars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compactar y refinar el gráfico de ritmo comercial mediante pilares agrupados más anchos y redondeados.

**Architecture:** La estructura Angular y los cálculos permanecen intactos. El cambio se concentra en el CSS encapsulado del dashboard y una prueba de estilos renderizados que protege la geometría elegida.

**Tech Stack:** Angular 16, Jasmine/Karma, CSS Grid y `color-mix()`.

## Global Constraints

- Mantener los siete días visibles.
- Mantener Minorista y Cuenta corriente como series separadas.
- No agregar dependencias, commit, push ni deploy.

---

### Task 1: Regresión visual del gráfico

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`

- [ ] Escribir un spec de template que renderice días y compruebe que `.dual-chart` usa `display: grid`, tiene ancho máximo controlado y `.chart-bar` supera el radio actual de 8px.
- [ ] Ejecutar `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts` y confirmar que falla por los estilos actuales.

### Task 2: Pilares agrupados

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css`

- [ ] Convertir `.dual-chart` a grid de siete columnas, centrarlo y limitarlo a `980px`.
- [ ] Llevar las barras a `min(42px, 44%)`, usar radio `12px 12px 4px 4px` y gradientes semánticos.
- [ ] Añadir una superficie tenue a `.chart-day.is-selected` sin alterar `aria-pressed` ni el click.
- [ ] En `max-width: 760px`, liberar el ancho máximo y usar siete columnas fluidas para conservar todos los días.
- [ ] Ejecutar el spec focal y confirmar GREEN.

### Task 3: Verificación

- [ ] Ejecutar `npm test -- --watch=false --browsers=ChromeHeadless` y confirmar cero fallos.
- [ ] Ejecutar `npm run build -- --configuration development` y confirmar exit code 0.
- [ ] Ejecutar `git diff --check` y revisar que el diff solo contenga el ajuste solicitado y sus tests.
