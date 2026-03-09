# EITI Backend

Backend de EITI en .NET con arquitectura por capas y enfoque Vertical Slice para casos de uso.

## Utilidad del proyecto
- Expone la API REST principal consumida por el frontend.
- Implementa reglas de negocio de ventas, cajas, seguridad, usuarios y roles.
- Gestiona persistencia de datos, autenticación/autorización y validaciones.

## Proyectos de la solución
- `eiti.Api`: capa de entrada HTTP (controllers/endpoints, configuración, middlewares).
- `eiti.Application`: casos de uso, comandos/queries, DTOs y lógica de aplicación.
- `eiti.Domain`: entidades, value objects y reglas de dominio.
- `eiti.Infrastructure`: acceso a datos, implementaciones técnicas, integraciones externas.
- `eiti.Tests`: pruebas automáticas.

## Arquitectura
- Patrón Vertical Slice para nuevas features/endpoints.
- Separación de responsabilidades por proyecto para mantener escalabilidad y mantenibilidad.

## Requisitos
- .NET SDK 8+
- SQL Server (o motor configurado en la cadena de conexión)

## Ejecución local
Desde la raíz `C:\Eiti\eiti`:

```bash
dotnet restore
dotnet build
dotnet run --project eiti.Api
```

## Pruebas
```bash
dotnet test
```
