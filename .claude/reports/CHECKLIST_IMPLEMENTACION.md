# Checklist de Implementación - Mejoras de Calidad de Código

## FASE 1: URGENTE (3-4 horas)

### 1.1 Reparar LoginHandler - Bare Catch Blocks ⏱️ 1-2 horas

**Archivo a modificar:** `eiti.Application/Features/Auth/Queries/Login/LoginHandler.cs`

**Opción A - Rápida (30 min):**
- [ ] Cambiar `catch` a `catch (ArgumentException)` en línea 43
- [ ] Cambiar `catch` a `catch (ArgumentException)` en línea 55
- [ ] Añadir logging de errores si es posible

**Opción B - Correcta (1-2 horas):**
- [ ] Crear método `GetByUsernameOrEmailAsync` en `IUserRepository`
- [ ] Implementación en `UserRepository` (SQL)
- [ ] Usar el nuevo método en LoginHandler
- [ ] Eliminar try-catch blocks
- [ ] Crear test para LoginHandler

**Test a crear:**
```csharp
[Fact]
public async Task Handle_ShouldAuthenticateByUsername_WhenProvidedValid()
[Fact]
public async Task Handle_ShouldAuthenticateByEmail_WhenUsernameNotFound()
[Fact]
public async Task Handle_ShouldRejectInactiveUser()
[Fact]
public async Task Handle_ShouldRejectInvalidPassword()
```

---

### 1.2 Crear Archivos *Errors.cs para Todos los Features ⏱️ 1-2 horas

**Estructura esperada (ya existe):**
```
eiti.Application/Features/
├── Auth/
│   └── Common/
│       └── RegisterErrors.cs ✅ EXISTE
│       └── LoginErrors.cs ✅ EXISTE
├── Sales/
│   ├── Commands/
│   │   ├── CreateSale/
│   │   │   └── CreateSaleErrors.cs ❌ FALTA - CREAR
│   │   ├── UpdateSale/
│   │   │   └── UpdateSaleErrors.cs ❌ FALTA - CREAR
│   │   └── SendSaleWhatsApp/
│   │       └── SendSaleWhatsAppErrors.cs ❌ FALTA - CREAR
├── CashSessions/
│   ├── Commands/
│   │   ├── OpenCashSession/
│   │   │   └── OpenCashSessionErrors.cs ❌ FALTA - CREAR
│   │   └── CloseCashSession/
│   │       └── CloseCashSessionErrors.cs ❌ FALTA - CREAR
```

**Para cada archivo:**
- [ ] Extraer todos los `Error.Validation(...)`, `Error.NotFound(...)`, etc. inline
- [ ] Crear constantes `public static readonly Error`
- [ ] Reemplazar en handlers

**Ejemplo CreateSaleErrors.cs:**
```csharp
public static readonly Error Unauthorized = ...
public static readonly Error InvalidStatus = ...
public static readonly Error PaymentForbidden = ...
public static readonly Error CancelNotAllowed = ...
public static readonly Error BranchNotFound = ...
public static readonly Error CustomerNotFound = ...
public static readonly Error NoDetails = ...
public static readonly Error InsufficientStock = ...
```

---

## FASE 2: IMPORTANTE (8-10 horas)

### 2.1 Refactorizar LoginHandler (Extensión) ⏱️ 0.5-1 hora

**Crear archivo:** `eiti.Application/Abstractions/Services/CurrentUserServiceExtensions.cs`

**Métodos a crear:**
- [ ] `EnsureAuthenticated()` - Valida IsAuthenticated
- [ ] `EnsureAuthenticatedWithContext()` - Valida IsAuthenticated + CompanyId + UserId
- [ ] `EnsureHasPermission()` - Valida permiso específico

**Usar en handlers:**
```csharp
// ANTES:
if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
    return Result.Failure(Error.Unauthorized(...));

// DESPUÉS:
var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
if (authCheck.IsFailure)
    return Result.Failure(authCheck.Error);
```

**Handlers a actualizar:**
- [ ] CloseCashSessionHandler
- [ ] OpenCashSessionHandler
- [ ] CreateSaleHandler
- [ ] Otros (búsqueda: "!_currentUserService.IsAuthenticated")

---

### 2.2 Extraer AuthenticationMapper ⏱️ 0.5-1 hora

**Crear archivo:** `eiti.Application/Features/Auth/Common/AuthenticationMapper.cs`

**Método:**
```csharp
public static (string[] Roles, string[] Permissions) MapRolesAndPermissions(User user)
```

**Usar en:**
- [ ] RegisterHandler (líneas 84-85)
- [ ] LoginHandler (líneas 84-85)

**Resultado:**
```csharp
// ANTES: 2-3 líneas en cada handler
var roles = user.Roles.Select(role => role.RoleCode).ToArray();
var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(permission => permission).ToArray();

// DESPUÉS: 1 línea en cada handler
var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);
```

---

### 2.3 Refactorizar CreateSaleHandler ⏱️ 4-6 horas

**Archivo:** `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleHandler.cs` (431 líneas)

**Objetivo:** Reducir a ~250 líneas, CC de ~15 a <5

**Paso 1: Extraer ValidateCreateSaleRequest() (líneas 51-78)**
- [ ] Nuevo método privado con 6 checks
- [ ] Retorna `Result` (not `Result<T>`)
- [ ] Usar `CreateSaleErrors.*` para constantes

```csharp
private Result ValidateCreateSaleRequest(CreateSaleCommand request)
{
    if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        return Result.Failure(CreateSaleErrors.InvalidStatus);
    // ... más checks
    return Result.Success();
}
```

**Paso 2: Extraer ValidateAndGroupSaleDetails() (líneas 98-140)**
- [ ] Nuevo método privado
- [ ] Valida: no vacío, sin duplicados, etc.
- [ ] Agrupa por ProductId

**Paso 3: Extraer ValidateAndGetBranch() (líneas 80-85)**
- [ ] Nuevo método privado
- [ ] Retorna `Branch?`

**Paso 4: Extraer ValidateAndGetCustomer() (líneas 87-96)**
- [ ] Nuevo método privado
- [ ] Retorna `Customer?`

**Paso 5: Extraer ProcessPayments() y ProcessTradeIns()**
- [ ] Extraer lógica de pagos a método privado
- [ ] Extraer lógica de trade-ins a método privado

**Resultado Handle():**
```csharp
public async Task<Result<CreateSaleResponse>> Handle(
    CreateSaleCommand request,
    CancellationToken cancellationToken)
{
    var authCheck = _currentUserService.EnsureAuthenticated();
    if (authCheck.IsFailure)
        return Result<CreateSaleResponse>.Failure(authCheck.Error);

    var requestValidation = ValidateCreateSaleRequest(request);
    if (requestValidation.IsFailure)
        return Result<CreateSaleResponse>.Failure(requestValidation.Error);

    var branch = await ValidateAndGetBranchAsync(...);
    if (branch is null)
        return Result<CreateSaleResponse>.Failure(CreateSaleErrors.BranchNotFound);

    var customer = await ValidateAndGetCustomerAsync(...);

    var details = ValidateAndGroupSaleDetails(request.Details);
    if (details.IsFailure)
        return Result<CreateSaleResponse>.Failure(details.Error);

    // ... crear sale

    return Result<CreateSaleResponse>.Success(...);
}
```

**Tests a crear:**
- [ ] Test: Rejects invalid status
- [ ] Test: Rejects without permission to pay
- [ ] Test: Validates branch exists
- [ ] Test: Validates customer exists
- [ ] Test: Validates details not empty

---

### 2.4 Refactorizar UpdateSaleHandler ⏱️ 4-5 horas

**Archivo:** `eiti.Application/Features/Sales/Commands/UpdateSale/UpdateSaleHandler.cs` (503 líneas)

**Similar a CreateSaleHandler:**
- [ ] Crear UpdateSaleErrors.cs
- [ ] Extraer ValidateUpdateSaleRequest()
- [ ] Extraer métodos para cada validación lógica
- [ ] Reducir de 503 a ~300 líneas

---

### 2.5 Mejorar ResultExtensions ⏱️ 0.5 horas

**Archivo:** `eiti.Api/Extensions/ResultExtensions.cs`

**Cambio:**
- [ ] Eliminar duplicación entre ToProblemDetails() (líneas 25-33) y GetTitle() (líneas 47-58)
- [ ] Crear método `GetErrorMapping()` que retorna `(int StatusCode, string Title)`
- [ ] Usar en ambos lugares

**Antes:**
```csharp
var statusCode = result.Error.Type switch { ... };
return new ObjectResult(new ProblemDetails
{
    Title = GetTitle(result.Error.Type),
    // ...
})

private static string GetTitle(ErrorType errorType)
{
    return errorType switch { ... };
}
```

**Después:**
```csharp
var (statusCode, title) = GetErrorMapping(result.Error.Type);
return new ObjectResult(new ProblemDetails
{
    Status = statusCode,
    Title = title,
    // ...
})

private static (int StatusCode, string Title) GetErrorMapping(ErrorType errorType)
{
    return errorType switch
    {
        ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error"),
        // ...
    };
}
```

---

## FASE 3: MANTENIMIENTO (4-6 horas)

### 3.1 Escribir Tests para Validators ⏱️ 1-2 horas

**Crear:**
- [ ] `RegisterValidatorTests.cs` (validar username, email, password, companyName)
- [ ] `LoginValidatorTests.cs` (validar input)
- [ ] `CreateSaleValidatorTests.cs` (validar detalles, pagos, etc.)

**Ejemplo estructura:**
```csharp
public sealed class RegisterValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectEmptyUsername() { }

    [Fact]
    public void Validate_ShouldRejectUsernameTooShort() { }

    [Fact]
    public void Validate_ShouldRejectInvalidEmail() { }

    [Fact]
    public void Validate_ShouldRejectWeakPassword() { }
}
```

---

### 3.2 Escribir Tests para CashSession Handlers ⏱️ 1-2 horas

**Crear:**
- [ ] `OpenCashSessionHandlerTests.cs`
- [ ] `CloseCashSessionHandlerTests.cs`
- [ ] `CreateCashWithdrawalHandlerTests.cs`

**Cobertura:**
- [ ] Caso feliz (happy path)
- [ ] Usuario no autenticado
- [ ] Drawer no existe o inactivo
- [ ] Ya hay sesión abierta (para Open)
- [ ] Cálculos de totales (para Close)

---

### 3.3 Agregar ConfigureAwait(false) ⏱️ 0.5-1 hora

**Archivos a actualizar:**
- [ ] Todos los handlers en `eiti.Application/Features/**/*.cs`
- [ ] Behavioral classes en `eiti.Application/Common/Behaviors/*.cs`

**Cambio:**
```csharp
// ANTES:
await _unitOfWork.SaveChangesAsync(cancellationToken);
await _userRepository.AddAsync(user, cancellationToken);

// DESPUÉS:
await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
```

**Razón:** Prevenir context capture innecesario en library code

---

### 3.4 Documentación de Arquitectura ⏱️ 1-2 horas

**Crear:**
- [ ] `ARCHITECTURE.md` en raíz del proyecto
- [ ] Diagrama de capas
- [ ] Ejemplo de flujo end-to-end
- [ ] Patrones usados (CQRS, Repository, UnitOfWork)
- [ ] Guía de contribución

---

## VALIDACIÓN Y VERIFICACIÓN

### Antes de Commit

Para **cada cambio**, verificar:

- [ ] **Compila sin errores** (dotnet build)
- [ ] **Tests pasan** (dotnet test)
- [ ] **Resharper/Code Analyzer limpio** (sin warnings)
- [ ] **Cambios coherentes** (no mezclados)

### Métricas a Validar

Después de completar las fases:

**Phase 1 Complete:**
```
- LoginHandler: No bare catch blocks
- Todos los features: Archivo *Errors.cs
- Verificar: grep "Error.Validation" no encontrado en handlers
```

**Phase 2 Complete:**
```
- CreateSaleHandler: ~250 líneas (fue 431)
- CreateSaleHandler CC: <5 (fue ~15)
- Duplicación reducida 50%
- Tests para CreateSale: ✅ completos
```

**Phase 3 Complete:**
```
- Test coverage: >65% (fue 49%)
- No bare await (sin ConfigureAwait(false)) en Application layer
- Documentación actualizada
```

---

## SEGUIMIENTO DE PROGRESO

### Tabla de Tareas

| # | Tarea | Responsable | Prioridad | Estado | Horas |
|---|-------|-------------|-----------|--------|-------|
| 1.1 | LoginHandler reparación | | P0 | ⬜ | 1-2h |
| 1.2 | Crear *Errors.cs | | P0 | ⬜ | 1-2h |
| 2.1 | CurrentUserServiceExt | | P1 | ⬜ | 0.5h |
| 2.2 | AuthenticationMapper | | P1 | ⬜ | 0.5h |
| 2.3 | CreateSaleHandler | | P1 | ⬜ | 4-6h |
| 2.4 | UpdateSaleHandler | | P1 | ⬜ | 4-5h |
| 2.5 | ResultExtensions | | P1 | ⬜ | 0.5h |
| 3.1 | Tests Validators | | P2 | ⬜ | 1-2h |
| 3.2 | Tests CashSession | | P2 | ⬜ | 1-2h |
| 3.3 | ConfigureAwait | | P2 | ⬜ | 0.5h |
| 3.4 | Documentación | | P2 | ⬜ | 1-2h |

---

## RECURSOS ÚTILES

### Archivos Clave
- Report: `/c/Eiti/eiti/.claude/quality_review_report.md`
- Ejemplos: `/c/Eiti/eiti/.claude/refactoring_examples.md`
- Resumen: `/c/Eiti/eiti/.claude/RESUMEN_EJECUTIVO.txt`

### Comandos Útiles
```bash
# Verificar complejidad ciclomática
# (requiere herramientas adicionales)

# Correr tests
dotnet test eiti.Tests.csproj

# Compilar
dotnet build

# Analizar con Roslyn Analyzer
dotnet build --verbosity diagnostic
```

---

**Última actualización:** 2026-03-16
**Generado por:** Quality Code Review Agent
