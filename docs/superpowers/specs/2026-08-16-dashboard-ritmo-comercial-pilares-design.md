# Ritmo comercial con pilares agrupados

## Objetivo

Mejorar la lectura del gráfico de los últimos siete días sin cambiar datos, filtros ni interacción.

## Diseño aprobado

- Conservar barras agrupadas para Minorista y Cuenta corriente.
- Limitar y centrar el ancho útil del gráfico para evitar grandes vacíos entre días.
- Usar siete columnas de ancho controlado en desktop y columnas fluidas en mobile.
- Aumentar el ancho máximo de cada barra de 30px a 42px.
- Redondear claramente el remate superior y suavizar la base.
- Reemplazar rellenos planos por gradientes verticales discretos con brillo interior.
- Dar al día seleccionado una superficie tenue que agrupe valores, barras y etiqueta.
- Mantener estados de foco, atenuado, animación de entrada y reduced motion existentes.

## Restricciones

- No agregar librerías de gráficos.
- No cambiar el endpoint, cálculos, series, colores semánticos ni comportamiento de drill-down.
- No hacer commit, push ni deploy.
