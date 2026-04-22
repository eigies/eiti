# CODE REVIEW EXHAUSTIVO - EITI Backend (.NET 8, Clean Architecture)

> Generado: 2026-03-28 | Revisor: Claude Sonnet 4.6
> Stack: .NET 8, Clean Architecture, Vertical Slice, MediatR, FluentValidation, EF Core, SQL Server

## Puntuación General: 7.5/10

---

## Resumen Ejecutivo

El proyecto presenta una **arquitectura sólida y bien estructurada** con Clean Architecture, Vertical Slice, MediatR y SQL Server. Se identifica **excelente encapsulamiento en el dominio** y **separación de responsabilidades correcta**, pero existen **7 problemas críticos** y **múltiples oportunidades de mejora** en validación, error handling y duplicación de código.

---

## Fortalezas

### 1. Dominio Robusto ✅
- **Value Objects bien implementados**: `Username`, `Email`, `PasswordHash` con validaciones encapsuladas
- **Aggregate Roots correctamente diseñados**: `Sale` con invariantes fuertes y métodos que garantizan consistencia
- **Encapsulamiento perfecto**: Propiedades privadas, colecciones de solo lectura via `IReadOnlyCollection<T>`
- **Normalización de decimales**: Método `NormalizeAmount()` en Sale previene errores de precisión

**Referencia**: `eiti.Domain/Sales/Sale.cs` (líneas 605-608)

### 2. Clean Architecture e Inyección de Dependencias ✅
- Separación clara entre capas: Api → Application → Domain ← Infrastructure
- `DependencyInjection.cs` bien organizado en cada capa
- Abstracción de repositorios mediante interfaces
- UnitOfWork pattern implementado correctamente

### 3. MediatR + Validación con Behaviors ✅
- Pipeline behaviors correctos: `ValidationBehavior`, `AuthorizationBehavior`
- FluentValidation bien integrado — validadores auto-registrados por assembly
- Validación en dos capas: FluentValidation + domain assertions

**Referencia**:
- `eiti.Application/Common/Behaviors/ValidationBehavior.cs`
- `eiti.Application/Features/Sales/Commands/AddCcPayment/AddCcPaymentValidator.cs`

### 4. Manejo de Errores Centralizado ✅
- Result<T> pattern elegante con discriminación Success/Failure
- Error type enum explícito (`Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`)
- Controller extension convierte Result a ActionResult + ProblemDetails HTTP-estándar

**Referencia**:
- `eiti.Api/Extensions/ResultExtensions.cs`
- `eiti.Application/Common/Result.cs`

### 5. Autenticación y Autorización ✅
- JWT bearer token correctamente configurado
- `CurrentUserService` extrae claims limpiamente de HttpContext
- Permission-based authorization con `IRequirePermissions` interface
- Rate limiting en endpoints sensibles

**Referencia**: `eiti.Api/Services/CurrentUserService.cs`

### 6. EF Core Configurations Explícitas ✅
- `IEntityTypeConfiguration<T>` pattern — configuraciones aisladas por entidad
- Value object converters configurados manualmente
- Precisión decimal explícita: `decimal(18,2)` para columnas monetarias

---

## Problemas Críticos

### ❌ CRÍTICO #1: Método `EnsureAuthenticated()` faltante en ICurrentUserService

**Ubicación**: `eiti.Application/Abstractions/Services/ICurrentUserService.cs`

**Problema**: 85 handlers llaman a `_currentUserService.EnsureAuthenticated()` pero este método **no existe en la interfaz**, está en una clase de extensión. Rompe el contrato y el principio LSP.

**Impacto**: Tests con mocks fallarán. Violación del Liskov Substitution Principle.

**Solución**:
```csharp
public interface ICurrentUserService
{
    // ... existentes ...
    Result EnsureAuthenticated();
    Result EnsureAuthenticatedWithContext();
    Result EnsureHasPermission(string permission);
}
```

**Archivos afectados**: `AddCcPaymentHandler.cs` (línea 35), `RegisterHandler.cs` (línea 40+) y 83 handlers más.

---

### ❌ CRÍTICO #2: Handler con 10+ dependencias — Violación SRP

**Ubicación**: `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleHandler.cs` (líneas 20-58)

**Problema**: Constructor inyecta 12 dependencias:
```csharp
ICurrentUserService, IBranchRepository, ICustomerRepository, IProductRepository,
IBranchProductStockRepository, IStockMovementRepository, ISaleRepository,
ICashSessionRepository, IAddressRepository, IBankRepository, IChequeRepository, IUnitOfWork
```

**Impacto**: Difícil de testear, responsabilidades mixtas (queries, stock, cash, cheques), God Object antipattern.

**Solución**:
```csharp
// Extraer a servicio de orquestación:
public CreateSaleHandler(
    ICurrentUserService currentUserService,
    ISaleCreationService saleCreationService, // Orquesta todo lo demás
    IUnitOfWork unitOfWork)
```

---

### ❌ CRÍTICO #3: Validación Enum sin Type Safety

**Ubicación**: `eiti.Application/Features/Sales/Commands/AddCcPayment/AddCcPaymentValidator.cs` (línea 13)

```csharp
// ❌ Hardcoded — si se añade método de pago, falla silenciosamente
RuleFor(x => x.IdPaymentMethod)
    .InclusiveBetween(1, 5).WithMessage("A valid payment method is required.");
```

**Solución**:
```csharp
RuleFor(x => x.IdPaymentMethod)
    .Must(id => Enum.IsDefined(typeof(SalePaymentMethod), id))
    .WithMessage("A valid payment method is required.");
```

---

### ❌ CRÍTICO #4: Riesgo SQL Injection en SearchDeliveryAddresses

**Ubicación**: `eiti.Infrastructure/Persistence/Repositories/SaleRepository.cs`

**Problema**: Búsqueda con LIKE sin validar la entrada antes de llegar al repositorio.

**Solución** — Validar en el handler/validator:
```csharp
if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 200)
    return Result<T>.Failure(Error.Validation("Query", "Invalid search query."));
```

---

### ❌ CRÍTICO #5: JWT Secret hardcodeado en appsettings.json

**Ubicación**: `eiti.Api/appsettings.json` (línea 6)

```json
"Secret": "replace-with-a-secure-key-at-least-32-characters-long"
```

**Impacto**: Credenciales expuestas en control de versiones.

**Solución**:
```csharp
// En Program.cs — fail-fast si no está configurado correctamente:
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
if (string.IsNullOrWhiteSpace(jwtSettings?.Secret) || jwtSettings.Secret.Contains("replace-with"))
    throw new InvalidOperationException("JWT Secret must be configured via environment variables or secrets manager.");
```

```json
// appsettings.json — solo placeholder para documentar la key:
"JwtSettings": {
  "Secret": "" // Set via environment variable JWT__Secret or User Secrets
}
```

---

### ❌ CRÍTICO #6: GlobalExceptionHandlingMiddleware Incompleto

**Ubicación**: `eiti.Api/Middleware/GlobalExceptionHandlingMiddleware.cs` (líneas 39-61)

**Problema**: Solo maneja `ValidationException`. `InvalidOperationException` desde domain aggregates devuelve 500.

**Solución**:
```csharp
private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
{
    var (statusCode, message) = exception switch
    {
        ValidationException validationEx => (StatusCodes.Status400BadRequest, validationEx.Message),
        InvalidOperationException invalidOpEx => (StatusCodes.Status400BadRequest, invalidOpEx.Message),
        ArgumentException argEx => (StatusCodes.Status400BadRequest, argEx.Message),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
    // ...
}
```

---

### ❌ CRÍTICO #7: Sale.Details Collection Puede ser Modificada Externamente

**Ubicación**: `eiti.Domain/Sales/Sale.cs` (líneas 36-40)

```csharp
private readonly List<SaleDetail> _details = [];
public IReadOnlyCollection<SaleDetail> Details => _details; // ⚠️ SaleDetail podría ser mutable
```

**Problema**: Si `SaleDetail` tiene propiedades mutables, el aggregate no sabe que fue modificado.

**Solución**:
```csharp
public IReadOnlyCollection<SaleDetail> Details => _details.AsReadOnly();
```

---

## Problemas de Prioridad Media

### 🟡 Duplicación Masiva en Response Mapping

**Problema**: Sin AutoMapper, 185+ features repiten el mismo patrón de mapping manualmente.

```csharp
// Repetido en 50+ handlers:
return Result<AddCcPaymentResponse>.Success(
    new AddCcPaymentResponse(
        payment.Id.Value,
        payment.SaleId.Value,
        (int)payment.Method,
        payment.Method.ToString(),
        // ... 8 propiedades más
    ));
```

**Solución — Extension methods de mapping**:
```csharp
// Application/Common/Mappings/SaleCcPaymentMappings.cs
public static class SaleCcPaymentMappings
{
    public static AddCcPaymentResponse ToResponse(this SaleCcPayment payment) =>
        new(payment.Id.Value, payment.SaleId.Value, (int)payment.Method, payment.Method.ToString(), ...);
}

// En handler:
return Result<AddCcPaymentResponse>.Success(payment.ToResponse());
```

---

### 🟡 Lógica de Negocio en Handlers en vez de Validators

**Problema**: Validaciones contextuales (ej: "Amount no puede exceder CcPendingAmount") están como `throw new InvalidOperationException(...)` en handlers, no en validators. Esto hace que sean tratadas como errores 500 en vez de 400.

**Solución**: Validadores con inyección de dependencias:
```csharp
public sealed class AddCcPaymentValidator : AbstractValidator<AddCcPaymentCommand>
{
    public AddCcPaymentValidator(ISaleRepository saleRepository)
    {
        RuleFor(x => x).MustAsync(async (cmd, ct) =>
        {
            var sale = await saleRepository.GetByIdAsync(new SaleId(cmd.SaleId), ct);
            return sale is not null && cmd.Amount <= sale.CcPendingAmount;
        }).WithMessage("Payment amount cannot exceed remaining balance.");
    }
}
```

---

### 🟡 Falta Logging en Handlers

**Problema**: Sin logs, imposible auditar comandos ejecutados, quién, cuándo.

**Solución**: Inyectar `ILogger<T>` en handlers y loguear entrada + errores de negocio:
```csharp
_logger.LogInformation(
    "User {UserId} adding CC payment to sale {SaleId}: Amount {Amount}",
    _currentUserService.UserId, request.SaleId, request.Amount);
```

---

### 🟡 Orden Incorrecto del Middleware

**Ubicación**: `eiti.Api/Program.cs` (líneas 99-113)

```csharp
// ❌ El middleware de excepciones DEBE ir al final
app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); // ← PRIMERO (mal)
app.UseHttpsRedirection();
app.UseCors("AllowLocalDevelopment");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
```

```csharp
// ✅ Correcto
app.UseHttpsRedirection();
app.UseCors("AllowLocalDevelopment");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>(); // ← AL FINAL
```

---

## Problemas de Baja Prioridad

### 🟢 Falta Paginación en Queries

Múltiples queries listan `IReadOnlyList<Sale>` sin límite. Con crecimiento de datos pueden traer 100k+ registros a memoria.

**Solución**:
```csharp
public async Task<PagedResult<Sale>> ListByCompanyAsync(
    CompanyId companyId, int pageNumber = 1, int pageSize = 50,
    CancellationToken cancellationToken = default)
{
    var query = _context.Sales
        .Where(s => s.CompanyId == companyId)
        .OrderByDescending(s => s.CreatedAt);

    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return new PagedResult<Sale>(items, totalCount, pageNumber, pageSize);
}
```

---

### 🟢 Faltan Índices de Base de Datos

Sin índices en columnas de búsqueda frecuente (`CompanyId`, `BranchId`, `CreatedAt`), las queries serán lentas en producción.

```csharp
builder.Entity<Sale>(entity =>
{
    entity.HasIndex(s => s.CompanyId);
    entity.HasIndex(s => s.BranchId);
    entity.HasIndex(s => new { s.CompanyId, s.CreatedAt });
});
```

---

### 🟢 Test Coverage Incompleto

185+ features con ~7 test files. Cobertura estimada: <5%.

**Estructura sugerida**:
```
eiti.Tests/
├── Unit/
│   ├── Features/
│   │   ├── Sales/
│   │   │   ├── CreateSaleHandlerTests.cs
│   │   │   ├── AddCcPaymentHandlerTests.cs
│   │   │   └── CancelSaleHandlerTests.cs
│   │   └── Customers/
│   └── Domain/
│       ├── SaleTests.cs
│       └── SaleCcPaymentTests.cs
└── Integration/
    └── SalesControllerTests.cs
```

---

## Matriz de Recomendaciones

| Prioridad | Problema | Esfuerzo | Impacto |
|-----------|----------|----------|---------|
| **ALTA** | Método faltante en ICurrentUserService | 2h | Crítico |
| **ALTA** | Handler con 10+ dependencias (CreateSale) | 1d | Crítico |
| **ALTA** | Validación enum hardcoded | 4h | Crítico |
| **ALTA** | SQL Injection en SearchDeliveryAddresses | 2h | Crítico |
| **ALTA** | JWT Secret hardcodeado | 3h | Crítico |
| **ALTA** | Middleware de excepciones incompleto | 4h | Alto |
| **ALTA** | Sale.Details mutable externamente | 2h | Alto |
| **MEDIA** | Duplicación de mapping en 50+ handlers | 2d | Alto |
| **MEDIA** | Lógica de negocio en handlers (no validators) | 1d | Medio |
| **MEDIA** | Falta logging en handlers | 1d | Medio |
| **MEDIA** | Orden incorrecto del middleware | 1h | Bajo |
| **BAJA** | Paginación faltante en queries | 2d | Medio |
| **BAJA** | Índices de base de datos faltantes | 4h | Medio |
| **BAJA** | Test coverage < 5% | 3d | Alto |

---

## Plan de Acción

### Semana 1 — Crítico (14-18 horas)
1. Mover `EnsureAuthenticated` a la interfaz `ICurrentUserService`
2. Refactorizar `CreateSaleHandler`: extraer `ISaleCreationService`
3. Arreglar validación de enums en todos los validators
4. Añadir validación de entrada en `SearchDeliveryAddresses`
5. Configurar JWT Secret desde environment variables + fail-fast check

### Semana 2 — Importante (12-15 horas)
6. Mejorar `GlobalExceptionHandlingMiddleware` con más handlers
7. Asegurar inmutabilidad de `Sale.Details`
8. Crear extension methods de mapping para eliminar duplicación
9. Corregir orden del middleware en `Program.cs`

### Roadmap (Sprints futuros)
- Logging en todos los handlers
- Validadores con contexto (repositorios inyectados)
- Paginación en queries
- Índices de base de datos
- Test coverage > 60%
- Documentación XML en controllers

---

## Conclusión

El backend tiene una **base arquitectónica sólida** que indica buen conocimiento de Clean Architecture y DDD. Los problemas identificados son principalmente de **seguridad puntual** (JWT, SQL Injection), **contratos rotos** (ICurrentUserService) y **mantenibilidad** (duplicación, SRP). Son todos corregibles en 1-2 sprints sin afectar la arquitectura existente.
