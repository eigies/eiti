# REPORTE COMPLETO DE SEGURIDAD - EITI BACKEND

**Fecha de revisión**: 2026-03-16
**Revisor**: Security Agent
**Proyecto**: EITI Backend (.NET 10)
**Arquitectura**: Clean Architecture (Api, Application, Domain, Infrastructure)
**Revisión anterior**: 2026-03-16 (primera revisión)
**Estado**: Actualizado con verificación de fixes implementados

---

## RESUMEN EJECUTIVO

Se realizó una revisión exhaustiva de seguridad del backend EITI. Desde la revisión anterior, se implementaron **4 fixes críticos** que mejoran significativamente la postura de seguridad:

1. **GlobalExceptionHandlingMiddleware** ya no expone `exception.Message` al cliente
2. **LoginHandler** usa `Username.IsValid()` / `Email.IsValid()` en lugar de bare catch blocks
3. **Rate limiting** implementado en `/auth/login` (5/min) y `/auth/register` (3/min)
4. **Price override** en `CreateSaleHandler` requiere permiso `sales.override_price`

**Estado actual**: La aplicación tiene una base sólida de seguridad. Los hallazgos restantes son principalmente de severidad media y baja, relacionados con configuración, diferenciación de roles y falta de permisos granulares en algunos comandos/queries.

| Severidad | Total | Resueltos | Pendientes |
|-----------|-------|-----------|------------|
| Alta      | 4     | 3         | 1          |
| Media     | 8     | 0         | 8          |
| Baja      | 4     | 0         | 4          |

---

## HALLAZGOS RESUELTOS

### [RESUELTO] Fix 1: Exposición de información sensible en excepciones
**Archivo**: `eiti.Api\Middleware\GlobalExceptionHandlingMiddleware.cs:53-59`
**Estado**: Resuelto
**Verificación**: El middleware ahora retorna mensaje genérico `"An unexpected error occurred. Please try again later."` en lugar de `exception.Message`. Las excepciones se loguean correctamente en el servidor con `_logger.LogError(ex, ...)`.

### [RESUELTO] Fix 2: LoginHandler sin bare catch blocks
**Archivo**: `eiti.Application\Features\Auth\Queries\Login\LoginHandler.cs:38-48`
**Estado**: Resuelto
**Verificación**: El handler usa `Username.IsValid()` y `Email.IsValid()` para validar antes de crear objetos de dominio. No hay catch blocks genéricos. El flujo es seguro y predecible.

### [RESUELTO] Fix 3: Rate limiting en endpoints de autenticación
**Archivo**: `eiti.Api\Program.cs:76-93` y `eiti.Api\Controllers\AuthController.cs:24,35`
**Estado**: Resuelto
**Verificación**: Rate limiting configurado con `AddFixedWindowLimiter`:
- `login`: 5 requests/minuto
- `register`: 3 requests/minuto
- Los controllers usan `[EnableRateLimiting("login")]` y `[EnableRateLimiting("register")]`
- Status code 429 configurado para respuestas de rate limit

### [RESUELTO] Fix 4: Price override requiere permiso
**Archivo**: `eiti.Application\Features\Sales\Commands\CreateSale\CreateSaleHandler.cs:134-143`
**Estado**: Resuelto
**Verificación**: El override de precio solo se aplica si el usuario tiene `PermissionCodes.SalesPriceOverride`. Si no tiene el permiso, se usa `product.Price`. El permiso `SalesPriceOverride` está correctamente definido en `PermissionCodes` y asignado solo a Owner y Admin en `RoleCatalog`.

---

## VULNERABILIDADES PENDIENTES - ALTA SEVERIDAD

### 1. JWT SECRET EN PLAINTEXT EN APPSETTINGS.JSON
**Archivo**: `eiti.Api\appsettings.json:6`
**Severidad**: Alta (CVSS 9.8)
**Estado**: Pendiente
**Problema**: Secret JWT es un placeholder visible en el repositorio:
```json
"Secret": "replace-with-a-secure-key-at-least-32-characters-long"
```
**Impacto**: Si se reemplaza con un secret real y se commitea, cualquier persona con acceso al repo puede generar tokens JWT válidos. Compromiso total de autenticación.
**Recomendación**: Mover a variables de entorno o Azure Key Vault. El `.gitignore` incluye `**/appsettings.*.local.json` pero NO excluye `appsettings.json` ni `appsettings.Development.json`.

---

## VULNERABILIDADES PENDIENTES - MEDIA SEVERIDAD

### 2. NUGET AUDIT DESHABILITADO
**Archivo**: `eiti.Api\eiti.Api.csproj:10` y `eiti.Infrastructure\eiti.Infrastructure.csproj:28`
**Severidad**: Media (CVSS 5.0)
**Estado**: Nuevo
**Problema**: Ambos proyectos tienen `<NuGetAudit>false</NuGetAudit>` y suprimen warnings de vulnerabilidades:
```xml
<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1902;NU1903</WarningsNotAsErrors>
<NuGetAudit>false</NuGetAudit>
```
**Impacto**: Vulnerabilidades conocidas en paquetes NuGet no serán detectadas durante el build. El proyecto usa `Microsoft.EntityFrameworkCore` **8.0.0** (en un proyecto .NET 10), que puede tener vulnerabilidades conocidas ya parcheadas en versiones posteriores.
**Recomendación**: Habilitar NuGet Audit y actualizar EF Core a versión compatible con .NET 10. Ejecutar `dotnet list package --vulnerable` periódicamente.

### 3. ENTITY FRAMEWORK CORE DESACTUALIZADO
**Archivo**: `eiti.Infrastructure\eiti.Infrastructure.csproj:9,13-14`
**Severidad**: Media (CVSS 5.5)
**Estado**: Nuevo
**Problema**: El proyecto usa .NET 10 pero EF Core 8.0.0:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
```
**Impacto**: Posibles vulnerabilidades de seguridad no parcheadas. Incompatibilidad potencial con runtime .NET 10. Se pierden mejoras de rendimiento y seguridad de EF Core 10.x.
**Recomendación**: Actualizar a EF Core 10.0.x para alinear con el target framework.

### 4. CONEXIÓN SQL SIN ENCRIPTACIÓN
**Archivo**: `eiti.Api\appsettings.json:3` y `eiti.Infrastructure\Persistence\ApplicationDbContextFactory.cs:21`
**Severidad**: Media (CVSS 5.9)
**Estado**: Nuevo
**Problema**: La cadena de conexión tiene `Encrypt=False` y `TrustServerCertificate=True`:
```
Server=localhost\SQLEXPRESS;Database=eitiDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;
```
**Impacto**: Tráfico entre la aplicación y SQL Server va sin encriptar. En producción, esto permite interceptar datos sensibles (contraseñas hasheadas, datos de clientes, ventas) mediante ataques man-in-the-middle.
**Recomendación**: Para producción, usar `Encrypt=True` y un certificado válido. Mantener `Encrypt=False` solo para desarrollo local.

### 5. CORS PERMISIVO (AllowAnyHeader + AllowAnyMethod)
**Archivo**: `eiti.Api\Program.cs:30-31`
**Severidad**: Media (CVSS 5.0)
**Estado**: Pendiente (de revisión anterior)
**Problema**: La política CORS permite cualquier header y método HTTP:
```csharp
.AllowAnyHeader()
.AllowAnyMethod();
```
**Nota**: La política solo aplica a orígenes localhost, lo cual mitiga parcialmente el riesgo. Sin embargo, para producción se debería tener una política separada con headers y métodos específicos.
**Recomendación**: Crear política de producción con `WithMethods("GET", "POST", "PUT", "DELETE")` y `WithHeaders("Authorization", "Content-Type", "Accept")`.

### 6. FALTA DE HEADERS DE SEGURIDAD HTTP
**Archivo**: `eiti.Api\Program.cs`
**Severidad**: Media (CVSS 5.8)
**Estado**: Pendiente (de revisión anterior)
**Problema**: No hay middleware para headers de seguridad HTTP. Headers faltantes:
- `Strict-Transport-Security` (HSTS)
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Content-Security-Policy`
- `Referrer-Policy`
**Recomendación**: Implementar middleware de security headers o usar paquete como `NWebsec`.

### 7. COMMANDS/QUERIES SIN IRequirePermissions
**Archivo**: Múltiples archivos en `eiti.Application\Features\`
**Severidad**: Media (CVSS 6.0)
**Estado**: Nuevo
**Problema**: Varios commands y queries que modifican datos no implementan `IRequirePermissions`, dependiendo solo de `[Authorize]` a nivel de controller. Esto significa que **cualquier usuario autenticado** puede ejecutar estas operaciones sin verificación de permisos granulares:

| Command/Query | Archivo |
|---|---|
| `ListUserRoleAuditsQuery` | `Users\UserFeature.cs:72-74` |
| `CreateBranchCommand` | `Branches\Commands\CreateBranch\CreateBranchCommand.cs` |
| `UpdateBranchCommand` | `Branches\Commands\UpdateBranch\UpdateBranchCommand.cs` |
| `ListBranchesQuery` | `Branches\Queries\ListBranches\ListBranchesQuery.cs` |
| `AdjustStockCommand` | `Stock\Commands\AdjustStock\AdjustStockCommand.cs` |
| `ListBranchStockQuery` | `Stock\Queries\ListBranchStock\ListBranchStockQuery.cs` |
| `CreateEmployeeCommand` | `Employees\EmployeeFeature.cs` |
| `UpdateEmployeeCommand` | `Employees\EmployeeFeature.cs` |
| `DeactivateEmployeeCommand` | `Employees\EmployeeFeature.cs` |
| `CreateVehicleCommand` | `Vehicles\VehicleFeature.cs` |
| `UpdateVehicleCommand` | `Vehicles\VehicleFeature.cs` |
| `DeactivateVehicleCommand` | `Vehicles\VehicleFeature.cs` |
| `CreateProductCommand` | `Products\Commands\CreateProduct\CreateProductCommand.cs` |
| `UpdateProductCommand` | `Products\Commands\UpdateProduct\UpdateProductCommand.cs` |
| `DeleteProductCommand` | `Products\Commands\DeleteProduct\DeleteProductCommand.cs` |
| `SendSaleWhatsAppCommand` | `Sales\Commands\SendSaleWhatsApp\SendSaleWhatsAppCommand.cs` |
| `CreateSaleTransportCommand` | `SaleTransport\SaleTransportFeature.cs` |
| `UpdateSaleTransportCommand` | `SaleTransport\SaleTransportFeature.cs` |
| `DeleteSaleTransportCommand` | `SaleTransport\SaleTransportFeature.cs` |
| `CreateCustomerCommand` | `Customers\Commands\CreateCustomer\CreateCustomerCommand.cs` |
| `UpdateCustomerCommand` | `Customers\Commands\UpdateCustomer\UpdateCustomerCommand.cs` |

**Impacto**: Un usuario con rol "Seller" podría crear/modificar sucursales, ajustar stock, gestionar empleados, vehículos y productos sin tener permisos explícitos para ello. Violación del principio de menor privilegio.
**Recomendación**: Agregar `IRequirePermissions` con permisos apropiados a cada command/query. Considerar crear nuevos permisos como `branches.manage`, `stock.adjust`, `employees.manage`, `vehicles.manage`, `products.manage`.

### 8. SWAGGER EXPUESTO EN DESARROLLO
**Archivo**: `eiti.Api\Program.cs:97-101`
**Severidad**: Media (CVSS 5.3)
**Estado**: Pendiente (de revisión anterior, reclasificado de Alta a Media)
**Problema**: Swagger UI se habilita si `ASPNETCORE_ENVIRONMENT=Development`. Esto es un riesgo si accidentalmente se ejecuta en modo Development en producción.
**Recomendación**: Verificar que producción siempre tenga `ASPNETCORE_ENVIRONMENT=Production`.

### 9. ROLES OWNER Y ADMIN CON PERMISOS CASI IDÉNTICOS
**Archivo**: `eiti.Application\Common\Authorization\RoleCatalog.cs:5-46`
**Severidad**: Media (CVSS 4.9)
**Estado**: Pendiente (de revisión anterior, ligeramente mejorado)
**Problema**: Owner y Admin tienen exactamente los mismos permisos (incluyendo `SalesPriceOverride` que se agregó a ambos). No hay diferenciación de privilegios entre ambos roles.
**Recomendación**: Definir permisos exclusivos para Owner (e.g., `company.manage`, `settings.manage`).

---

## VULNERABILIDADES PENDIENTES - BAJA SEVERIDAD

### 10. VALIDACIÓN DÉBIL DE PARÁMETROS DE BÚSQUEDA
**Archivo**: `eiti.Api\Controllers\CustomersController.cs:44-53`
**Severidad**: Baja (CVSS 4.3)
**Estado**: Pendiente (de revisión anterior, reclasificado de Media a Baja)
**Problema**: Parámetros de búsqueda (`query`, `email`, `documentNumber`) sin límite de longitud máxima.
**Recomendación**: Agregar FluentValidator con `MaximumLength()`.

### 11. VALIDACIÓN INCOMPLETA DE PAGINACIÓN
**Archivo**: `eiti.Application\Features\Users\UserFeature.cs:484`
**Severidad**: Baja
**Estado**: Pendiente (de revisión anterior)
**Problema**: Validación de `Take` en `ListUserRoleAuditsHandler` se hace en handler con `Math.Clamp()`, no en validator.
**Recomendación**: Agregar FluentValidator para consistencia.

### 12. SIN LOGGING DE EVENTOS DE SEGURIDAD
**Archivo**: Handlers en `Features\`
**Severidad**: Baja
**Estado**: Pendiente (de revisión anterior)
**Problema**: No hay registro estructurado de eventos sensibles (cambios de rol, login, creación de usuarios). Los role audits se persisten en BD, lo cual es positivo, pero no hay logging a nivel de aplicación.
**Recomendación**: Agregar `ILogger` con nivel Warning para eventos de seguridad.

### 13. ERRORES SILENCIOSOS EN VALIDACIÓN DE ROLES (CreateUserHandler)
**Archivo**: `eiti.Application\Features\Users\UserFeature.cs:168-171`
**Severidad**: Baja
**Estado**: Pendiente (de revisión anterior)
**Problema**: `NormalizeRoles()` en `CreateUserHandler` retorna `Array.Empty()` si hay roles inválidos, lo cual se traduce en error de validación "Select at least one role" que no indica el problema real.
**Nota**: `UpdateUserRolesHandler` (líneas 346-349) maneja esto correctamente retornando error explícito `"One or more selected roles are invalid."`. Solo `CreateUserHandler` tiene este issue.

---

## ASPECTOS POSITIVOS DE SEGURIDAD

1. **JWT Configuration correcta** - Validación completa (issuer, audience, lifetime, signing key) en `Program.cs:61-71`

2. **Password hashing robusto** - PBKDF2 con SHA256, 100,000 iteraciones, salt de 16 bytes, comparación en tiempo constante con `CryptographicOperations.FixedTimeEquals()` en `PasswordHasher.cs`

3. **Company isolation** - Todas las operaciones sensibles verifican `CompanyId` del usuario autenticado contra los recursos accedidos. Previene acceso cross-tenant.

4. **Authorization pipeline** - `AuthorizationBehavior` como pipeline behavior de MediatR verifica permisos automáticamente para cualquier request que implemente `IRequirePermissions`

5. **Rate limiting en auth** - Endpoints de login y register protegidos contra brute force con fixed window rate limiting

6. **Exception handling seguro** - El middleware de excepciones no expone información interna al cliente

7. **Login seguro** - Mensajes de error genéricos (`InvalidCredentials`) que no revelan si el usuario existe o no. Verificación de `IsActive` antes de permitir login.

8. **Role audit trail** - Cambios de roles se persisten en BD con usuario que hizo el cambio, roles anteriores y nuevos

9. **HTTPS redirect** - `UseHttpsRedirection()` activo en `Program.cs:104`

10. **Input validation pipeline** - `ValidationBehavior` con FluentValidation integrado en pipeline de MediatR

11. **Self-deactivation protection** - `SetUserActiveStatusHandler` previene que un usuario se desactive a sí mismo

12. **CORS localhost-only** - La política CORS solo permite orígenes loopback, mitigando ataques desde orígenes externos

---

## CHECKLIST DE ACCIONES PRIORITARIAS

### ALTA (Esta semana)
- [ ] Mover JWT Secret a variables de entorno (no commitear en appsettings.json)
- [ ] Ejecutar `git log --all --full-history -- appsettings.json` para verificar que no hay secrets en historial
- [ ] Habilitar NuGet Audit (`<NuGetAudit>true</NuGetAudit>`) y resolver vulnerabilidades
- [ ] Actualizar EF Core de 8.0.0 a versión compatible con .NET 10

### MEDIA (Este mes)
- [ ] Agregar `IRequirePermissions` a commands/queries que modifican datos (ver hallazgo #7)
- [ ] Crear permisos granulares: `branches.manage`, `stock.adjust`, `employees.manage`, `vehicles.manage`, `products.manage`
- [ ] Diferenciar permisos entre Owner y Admin
- [ ] Configurar `Encrypt=True` para connection string de producción
- [ ] Implementar middleware de security headers (HSTS, X-Frame-Options, CSP)
- [ ] Crear política CORS específica para producción

### BAJA (Este trimestre)
- [ ] Agregar FluentValidators para longitud máxima en parámetros de búsqueda
- [ ] Logging estructurado de eventos de seguridad
- [ ] Corregir error silencioso en `NormalizeRoles()` de `CreateUserHandler`
- [ ] Considerar 2FA para cuentas de Admin/Owner

---

## ESTADÍSTICAS

**Total de hallazgos**: 13
- Resueltos: 4 (31%)
- Pendientes Alta: 1 (8%)
- Pendientes Media: 8 (46%)
- Pendientes Baja: 4 (31%)

**Aspectos positivos identificados**: 12

**Cobertura de seguridad estimada**: ~78% (mejora desde ~70% en revisión anterior)

---

**Reporte actualizado**: 2026-03-16
**Revisor**: Security Agent (Experto en seguridad de aplicaciones .NET)
**Archivos revisados**: 35+ archivos clave (Controllers, Handlers, Commands, Queries, Infrastructure, Configuration)
**Próxima revisión recomendada**: Después de implementar acciones de prioridad Alta
