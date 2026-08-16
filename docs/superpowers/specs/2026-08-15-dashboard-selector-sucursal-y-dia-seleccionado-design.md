# Selector de sucursal y día seleccionado del dashboard

## Objetivo

Recuperar dos convenciones visuales del dashboard sin modificar su contrato con el backend: usar el dropdown compartido de la aplicación para sucursales y mantener visible el día elegido en el gráfico.

## Diseño aprobado

- Reemplazar el `<select>` nativo por `app-searchable-select` en modo compacto.
- Mostrar `Todas` mediante `allowClear` y `clearValue = null` solamente cuando el usuario puede consultar todas las sucursales.
- Mantener la selección enlazada a `branchId`; una selección distinta conserva el flujo existente: guardar preferencia, limpiar exploración y recargar el resumen.
- Mostrar un chip con `selectedDayLabel` entre el encabezado y el contenido del dashboard mientras `selectedDayKey` tenga valor. Su botón elimina el filtro mediante `clearDay()`.
- Reforzar la barra seleccionada con borde, gradiente y sombra; las demás continúan atenuadas.
- El día no se persiste entre recargas ni cambios de sucursal. Solo permanece durante la exploración actual, como antes del rediseño.

## Accesibilidad y responsive

- El selector tendrá etiqueta accesible y texto de búsqueda explícito.
- El chip incluirá un botón con `aria-label` descriptivo.
- En pantallas angostas, selector y chip ocuparán el ancho disponible sin desbordar.

## Fuera de alcance

- Cambios de API o base de datos.
- Persistencia del filtro de día.
- Deploy, commit o integración de rama.
