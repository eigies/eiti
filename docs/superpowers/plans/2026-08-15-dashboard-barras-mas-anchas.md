# Dashboard Bars Width Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar mas cuerpo visual a las barras agrupadas del dashboard sin cambiar datos, escala, altura ni interaccion.

**Architecture:** Es un ajuste exclusivo de presentacion en el CSS del componente standalone del dashboard. Se conserva el limite porcentual para que las dos barras de cada dia sigan entrando en anchos reducidos.

**Tech Stack:** Angular 16, HTML/CSS del componente standalone, Karma/Jasmine, Angular CLI.

## Global Constraints

- Ancho maximo por barra: 30 px.
- Ancho relativo maximo por barra: 44% del espacio disponible para el dia.
- Separacion entre barras: 0.26 rem.
- No cambiar altura del grafico, datos, colores, animacion, seleccion ni breakpoints.
- No hacer deploy.
- No crear commits: ambos repositorios contienen el trabajo activo de la feature sin commitear.

---

### Task 1: Ensanchar las barras agrupadas

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css:369-397`
- Test: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.spec.ts`

**Interfaces:**
- Consumes: `.chart-day__values`, `.chart-day__plot` y `.chart-bar` existentes.
- Produces: barras de hasta 30 px que mantienen dos segmentos dentro de cada columna diaria.

- [x] **Step 1: Ejecutar los specs actuales del dashboard**

Run:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts
```

Expected: todos los specs de `DashboardComponent` pasan antes del cambio.

- [x] **Step 2: Aplicar el ajuste CSS minimo**

Cambiar el bloque compartido y el ancho de barra a:

```css
.chart-day__values,
.chart-day__plot {
  display: flex;
  justify-content: center;
  gap: .26rem;
}

.chart-bar {
  width: min(30px, 44%);
  min-height: 0;
  border-radius: 8px 8px 2px 2px;
  transform: scaleY(0);
  transform-origin: bottom;
  animation: grow-bar .6s var(--delay, 0s) cubic-bezier(.16, 1, .3, 1) both;
}
```

- [x] **Step 3: Ejecutar los specs del dashboard**

Run:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/dashboard/dashboard.component.spec.ts
```

Expected: todos los specs pasan.

- [x] **Step 4: Compilar el frontend**

Run:

```powershell
npm run build -- --configuration development
```

Expected: build correcto; se admiten solamente los warnings preexistentes de CommonJS y `align-items: end` fuera del dashboard.

- [x] **Step 5: Revisar responsive**

Verificar el dashboard con datos en desktop y mobile. Las barras deben verse mas anchas, conservar una separacion visible y no superponerse ni salir de su columna diaria.
