# Sucursales como chips en el dashboard

## Objetivo

Reemplazar el dropdown de sucursales por chips visibles que reutilicen el lenguaje visual de los toggles del gráfico.

## Diseño aprobado

- Mostrar `Todas` únicamente cuando `canViewAllBranches` sea verdadero.
- Mostrar una opción por cada sucursal disponible.
- Marcar el alcance activo con la misma superficie y color ámbar de los controles de la gráfica.
- Cada chip llama al flujo existente `setBranch`, por lo que conserva preferencia, limpieza de exploración y recarga del resumen.
- En desktop los chips pueden ocupar el espacio disponible y envolver si fuera necesario.
- En mobile permanecen en una sola fila con desplazamiento horizontal y sin cortar las etiquetas.
- Usar `role="group"`, `aria-label` y `aria-pressed` para comunicar el estado.

## Limpieza

- Eliminar `FormsModule`, `SearchableSelectComponent`, `SearchableSelectOption`, `branchOptions` y `onBranchSelected` del dashboard.
- Eliminar la prueba y el `z-index` agregados específicamente para el panel flotante del dropdown.

## Fuera de alcance

- Cambios al componente compartido `app-searchable-select`.
- Cambios de backend, persistencia adicional, commit o deploy.
