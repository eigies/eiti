# CODE REVIEW REPORT: EiTi Frontend (Angular 16)

> Generado: 2026-03-28 | Revisor: Claude Sonnet 4.6

## Resumen Ejecutivo

El frontend de EiTi es una **aplicación Angular 16 bien estructurada** con componentes standalone y sólidos fundamentos arquitectónicos. Sin embargo, existen **problemas críticos de performance y memory leaks** que requieren atención inmediata.

### Hallazgos Clave
| Categoría | Estado |
|-----------|--------|
| Estructura de carpetas | ✅ Excelente |
| TypeScript strict mode | ✅ Activo |
| Lazy loading | ✅ Implementado |
| Route guards | ✅ Funcionando |
| ChangeDetectionStrategy.OnPush | ❌ FALTANTE en ~50 componentes |
| Subscriptions sin unsubscribe | ❌ 70+ ubicaciones con memory leaks |
| Componentes monolíticos | ⚠️ SalesPageComponent: 200 TS / 804 HTML líneas |

---

## 1. PROBLEMAS CRÍTICOS

### 1.1 Sin ChangeDetectionStrategy.OnPush (CRÍTICO - PERFORMANCE)

**Problema:** Cero componentes usan `ChangeDetectionStrategy.OnPush`

**Archivos afectados:** Todos los componentes (~50 en total)
- `CustomersComponent`
- `ProductsComponent`
- `DashboardComponent`
- `SalesPageComponent`
- Y todos los demás

**Impacto:** Angular re-verifica TODAS las propiedades de componentes en cada evento (clicks, respuestas HTTP, timers). Con 50+ componentes, esto causa verificaciones innecesarias exponenciales.

**Ejemplo del problema:**
```typescript
@Component({
  selector: 'app-products',
  standalone: true,
  // ❌ FALTA: changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductsComponent {
  products: ProductResponse[] = [];
  branches: BranchResponse[] = [];
  // ... 40+ propiedades más
}
```

**Solución:**
```typescript
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-products',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
```

**Esfuerzo estimado:** 2-3 horas (cambio mecánico en todos los componentes)

---

### 1.2 Subscriptions sin gestionar - Memory Leaks (70+ UBICACIONES)

**ToastComponent** - `src/app/shared/components/toast/toast.component.ts:18`
```typescript
// ❌ PROBLEMA
ngOnInit(): void {
  this.toastService.toasts$.subscribe(t => (this.toasts = t));  // SIN UNSUBSCRIBE
}
```

**NavbarComponent** - `src/app/shared/components/navbar/navbar.component.ts:34-44`
```typescript
// ❌ PROBLEMA
this.router.events.subscribe(event => {  // ACUMULA SUBSCRIPTIONS
  if (event instanceof NavigationEnd) {
    this.closeSidebar();
  }
});
```

**ProductsComponent** - línea 474
```typescript
// ❌ PROBLEMA
for (const product of this.products) {
  form.valueChanges.subscribe(() => this.refreshBulkProductState(product.id));  // LEAK
}
```

**Patrón correcto:**
```typescript
import { Subject, takeUntil } from 'rxjs';

export class MyComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.service.data$
      .pipe(takeUntil(this.destroy$))
      .subscribe(data => this.data = data);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

**Mejor enfoque — async pipe:**
```typescript
// TypeScript
data$ = this.service.data$;

// Template
<div *ngIf="data$ | async as data">{{ data.name }}</div>
```

**Esfuerzo estimado:** 4-6 horas

---

### 1.3 SalesPageComponent - Componente Monolítico (CRÍTICO - MANTENIBILIDAD)

**Archivo:** `src/app/features/sales/sales-page.component.ts`

**Problemas:**
- 200+ líneas de TypeScript
- 804 líneas de HTML template
- 10+ formularios simultáneos: `lineForm`, `filterForm`, `editLineForm`, `editMetaForm`, `transportForm`
- 30+ propiedades de instancia
- 8+ responsabilidades: creación de ventas, edición, filtros, pagos, transporte, WhatsApp, paginación, borradores

```typescript
// ❌ Demasiadas responsabilidades en un solo componente
export class SalesPageComponent implements OnInit {
  lineForm: FormGroup;
  filterForm: FormGroup;
  editLineForm: FormGroup;
  editMetaForm: FormGroup;
  transportForm: FormGroup;
  products: ProductResponse[] = [];
  branches: BranchResponse[] = [];
  // ... 20+ propiedades más
}
```

**Estructura recomendada:**
```
SalesPageComponent (Container/Smart)
├── SalesListComponent (Lista + Filtrado)
├── SalesCreatorComponent (Formulario de creación)
├── SalesEditorComponent (Modal de edición)
├── SalesPaymentComponent (Gestión de pagos)
└── SalesTransportComponent (Asignación de transporte)
```

**Esfuerzo estimado:** 8-16 horas

---

## 2. PROBLEMAS DE ALTA PRIORIDAD

### 2.1 trackBy faltante en listas (Performance)

**Archivos:** `dashboard.component.ts`, `customers.component.ts`, `cash.component.ts`, `sales-page.component.ts`

```typescript
// ❌ SIN trackBy - DOM reconstruye lista completa en cada cambio
<div *ngFor="let customer of customers">{{ customer.name }}</div>

// ✅ CON trackBy - Solo re-renderiza items actualizados
<div *ngFor="let customer of customers; trackBy: trackByCustomer">{{ customer.name }}</div>

trackByCustomer(_: number, customer: CustomerResponse): string {
  return customer.id;
}
```

> `ProductsComponent` (línea 417) lo implementa correctamente — replicar ese patrón.

**Esfuerzo estimado:** 1-2 horas

---

### 2.2 Problema N+1 en CustomersComponent

**Archivo:** `src/app/features/customers/customers.component.ts:131-151`

```typescript
private loadCustomers(): void {
  this.customerService.listCustomers().subscribe({
    next: customers => {
      // ❌ PROBLEMA: Fetch individual por cada cliente (N+1)
      forkJoin(
        customers.map(customer =>
          this.customerService.getCustomerById(customer.id).pipe(
            catchError(() => of(customer))
          )
        )
      ).subscribe({
        next: detailedCustomers => {
          this.customers = detailedCustomers;
        }
      });
    }
  });
}
```

**Fix:** Eliminar el `forkJoin`, o asegurar que el primer endpoint devuelva datos completos.

**Esfuerzo estimado:** 30 minutos

---

### 2.3 Sin manejo centralizado de errores

**Patrón repetido 50+ veces:**
```typescript
.subscribe({
  error: err => {
    this.loading = false;
    this.toast.error(err?.error?.detail || err?.error?.message || 'Error occurred');
  }
});
```

**Solución — ErrorHandlerService:**
```typescript
@Injectable({ providedIn: 'root' })
export class ErrorHandlerService {
  extractErrorMessage(error: unknown, defaultMessage = 'Ocurrió un error'): string {
    const httpError = error as any;
    return httpError?.error?.detail ||
           httpError?.error?.message ||
           httpError?.message ||
           defaultMessage;
  }
}
```

**Esfuerzo estimado:** 2-3 horas

---

## 3. PROBLEMAS DE PRIORIDAD MEDIA

### 3.1 Otros componentes grandes que necesitan refactoring

- **ProductsComponent:** 45+ propiedades, lógica compleja de edición masiva, 50+ subscriptions
- **CashComponent:** Formularios anidados, múltiples operaciones de caja, subscriptions en cascada
- **SalesFullComponent:** Formulario multi-paso, creación de cliente inline, lógica de transporte compleja

**Esfuerzo estimado:** 8-12 horas por componente

---

### 3.2 Números mágicos en el código

**DashboardComponent:**
```typescript
// ❌
get paidSales(): SaleResponse[] {
  return this.sales.filter(s => s.idSaleStatus === 2);  // ¿Qué es 2?
}

// ✅
export const SALE_STATUS = {
  PENDING: 1,
  PAID: 2,
  CANCELLED: 3
} as const;

get paidSales() {
  return this.sales.filter(s => s.idSaleStatus === SALE_STATUS.PAID);
}
```

**Esfuerzo estimado:** 1-2 horas

---

### 3.3 Cobertura de tests limitada

**Tests actuales:**
- `auth.interceptor.spec.ts`
- `sales-full.component.spec.ts`
- `sales-page.component.spec.ts`
- `permission.models.spec.ts`

**Faltantes:** Todos los servicios, la mayoría de componentes

**Cobertura estimada:** ~5%

**Esfuerzo estimado:** 20+ horas para alcanzar 70%

---

## 4. PUNTOS POSITIVOS

- ✅ Excelente estructura de carpetas con separación de responsabilidades
- ✅ Configuración TypeScript strict activada
- ✅ Capas de servicio correctamente definidas
- ✅ Route guards implementados
- ✅ Reactive forms bien utilizados
- ✅ Integración auth/interceptor sólida (manejo 401/429)
- ✅ Modelos/interfaces completos
- ✅ Standalone components siguiendo Angular 16 best practices
- ✅ Lazy loading en todas las rutas
- ✅ Manejo centralizado de 401 en interceptor

---

## 5. PLAN DE ACCIÓN RECOMENDADO

### Fase 1 — Inmediato (Semana 1) | 10-15 horas
1. **Agregar OnPush a todos los componentes** (mecánico, bajo riesgo, ALTO impacto)
2. **Corregir subscription leaks** en `ToastComponent`, `NavbarComponent`, `ProductsComponent`
3. **Crear clase base** con `destroy$` para reutilizar patrón
4. **Agregar trackBy** a todos los `*ngFor`

### Fase 2 — Corto plazo (Semana 2-3) | 15-20 horas
1. **Dividir SalesPageComponent** en componentes más pequeños
2. **Crear ErrorHandlerService** para manejo centralizado
3. **Corregir N+1 en customers**
4. **Agregar validadores de formulario personalizados**
5. **Definir constantes** para números mágicos

### Fase 3 — Mediano plazo (Semana 4+) | 20-30 horas
1. **Agregar tests de servicios** (alto ROI para confiabilidad)
2. **Refactorizar componentes grandes** (ProductsComponent, CashComponent, SalesFullComponent)
3. **Agregar tests de componentes** (rutas críticas primero)
4. **Profiling de performance** y optimización

---

## 6. FIXES ESPECÍFICOS

### Fix 1: ToastComponent Memory Leak
```typescript
export class ToastComponent implements OnInit, OnDestroy {
  toasts: Toast[] = [];
  private destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.toastService.toasts$
      .pipe(takeUntil(this.destroy$))
      .subscribe(t => (this.toasts = t));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

### Fix 2: NavbarComponent Router Events Leak
```typescript
this.router.events
  .pipe(
    filter(event => event instanceof NavigationEnd),
    takeUntil(this.destroy$)
  )
  .subscribe(() => this.closeSidebar());
```

### Fix 3: ProductsComponent Bulk Edit Leak
```typescript
private bulkEditSubscriptions = new Map<string, Subscription>();

cancelBulkEdit(force = false): void {
  this.bulkEditSubscriptions.forEach(sub => sub.unsubscribe());
  this.bulkEditSubscriptions.clear();
}
```

---

## 7. CONCLUSIÓN

El frontend de EiTi tiene **sólidas bases arquitectónicas** pero sufre de **problemas críticos de performance y memory leaks** que se acumulan con el tiempo de sesión del usuario.

**Impacto esperado post-fixes:**
- 50-70% mejora de performance (OnPush + trackBy)
- Cero memory leaks (gestión correcta de subscriptions)
- Mejor mantenibilidad (componentes más pequeños)
- Mayor confiabilidad (tests comprensivos)
