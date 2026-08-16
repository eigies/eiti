# Lectura mensual con volumen

## Objetivo

Mejorar la jerarquía visual de "Lectura del mes en curso" sin cambiar sus márgenes, dimensiones generales, datos ni comportamiento. La tabla debe conservar su lectura comparativa y sentirse integrada con los pilares del gráfico de ritmo comercial.

## Diseño aprobado

- Conservar la estructura de tabla y sus cinco columnas cuando el usuario puede ver importes.
- Presentar cada segmento como una banda elevada dentro de la tabla: fondo cálido sutil, borde fino, esquinas redondeadas y una sombra interior mínima.
- Dar mayor presencia a la fila Total mediante contraste, peso tipográfico y un acento ámbar controlado.
- Mantener los colores semánticos existentes para Minorista y Cuenta corriente.
- Diferenciar visualmente las columnas del mes de las columnas de hoy con superficies apenas distintas, sin agregar nuevas etiquetas ni duplicar información.
- Mejorar el ritmo vertical de etiquetas e importes, manteniendo el ancho y los márgenes actuales del panel.
- Aplicar un hover discreto a las filas; no sumar animaciones ornamentales.
- En pantallas pequeñas, conservar la transformación actual a bloques de dos columnas y aplicar el mismo lenguaje de superficies sin aumentar el ancho.

## Fuera de alcance

- Cambios en API, modelos, cálculos o permisos financieros.
- Nuevos indicadores, gráficos o comparaciones porcentuales.
- Cambios en el tamaño exterior o en la ubicación del panel.
- Deploy, push o cambios de infraestructura.

## Criterios de aceptación

- Las tres filas se perciben como unidades elevadas y separadas.
- La fila Total domina visualmente sin competir con el encabezado del panel.
- Las celdas de "Mes" y "Hoy" se distinguen con contraste sutil.
- La tabla sigue siendo legible y funcional en desktop y mobile.
- Las pruebas existentes y el build de desarrollo continúan pasando.
