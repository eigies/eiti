# EJEMPLOS CONCRETOS DE REFACTORING

## Problema 1: LoginHandler - Excepciones Silenciosas

### Código Actual ❌

**Archivo:** `eiti.Application/Features/Auth/Queries/Login/LoginHandler.cs` (líneas 36-59)

```csharp
public async Task<Result<LoginResponse>> Handle(
    LoginQuery request,
    CancellationToken cancellationToken)
{
    User? user = null;

    try
    {
        var username = Username.Create(request.UsernameOrEmail);
        user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
    }
    catch  // ⚠️ PROBLEMA: Catch all sin logging
    {
        // Try as email.
    }

    if (user is null)
    {
        try
        {
            var email = Email.Create(request.UsernameOrEmail);
            user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        }
        catch  // ⚠️ PROBLEMA: Oculta errores de validación y BD
        {
            // Invalid input for both username and email.
        }
    }

    if (user is null)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    bool isValidPassword = _passwordHasher.VerifyPassword(
        request.Password,
        user.PasswordHash.Value);

    if (!isValidPassword)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    if (!user.IsActive)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    user.UpdateLastLogin();
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var token = _jwtTokenGenerator.GenerateToken(user);
    var roles = user.Roles.Select(role => role.RoleCode).ToArray();
    var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(permission => permission).ToArray();

    return Result<LoginResponse>.Success(
        new LoginResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            token,
            roles,
            permissions));
}
```

### Código Mejorado ✅

**Opción A: Mejorar el Handler actual**

```csharp
public async Task<Result<LoginResponse>> Handle(
    LoginQuery request,
    CancellationToken cancellationToken)
{
    User? user = await FindUserByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);

    if (user is null)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value))
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    if (!user.IsActive)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    user.UpdateLastLogin();
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var token = _jwtTokenGenerator.GenerateToken(user);
    var (roles, permissions) = BuildRoleAndPermissions(user);

    return Result<LoginResponse>.Success(
        new LoginResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            token,
            roles,
            permissions));
}

private async Task<User?> FindUserByUsernameOrEmailAsync(string input, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    // Intentar como username
    try
    {
        var username = Username.Create(input);
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is not null)
        {
            return user;
        }
    }
    catch (ArgumentException)
    {
        // Input no es un username válido, continuar con email
    }

    // Intentar como email
    try
    {
        var email = Email.Create(input);
        return await _userRepository.GetByEmailAsync(email, cancellationToken);
    }
    catch (ArgumentException)
    {
        // Input no es email válido tampoco
        return null;
    }
}

private (string[] Roles, string[] Permissions) BuildRoleAndPermissions(User user)
{
    var roles = user.Roles.Select(role => role.RoleCode).ToArray();
    var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(p => p).ToArray();
    return (roles, permissions);
}
```

**Opción B: Mejor aún - Delegar al Repository**

Crear en `IUserRepository`:

```csharp
public interface IUserRepository
{
    // Método nuevo: Busca por username O email
    Task<User?> GetByUsernameOrEmailAsync(string input, CancellationToken cancellationToken);
}
```

Implementación en EF Core:

```csharp
public async Task<User?> GetByUsernameOrEmailAsync(string input, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    return await _context.Users
        .FirstOrDefaultAsync(u =>
            u.Username.Value == input ||
            u.Email.Value == input,
            cancellationToken);
}
```

Luego el Handler es mucho más limpio:

```csharp
public async Task<Result<LoginResponse>> Handle(
    LoginQuery request,
    CancellationToken cancellationToken)
{
    // ✅ SIN excepciones, SIN try-catch
    var user = await _userRepository.GetByUsernameOrEmailAsync(
        request.UsernameOrEmail,
        cancellationToken);

    if (user is null || !user.IsActive)
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value))
    {
        return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
    }

    user.UpdateLastLogin();
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var token = _jwtTokenGenerator.GenerateToken(user);
    var (roles, permissions) = BuildRoleAndPermissions(user);

    return Result<LoginResponse>.Success(
        new LoginResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            token,
            roles,
            permissions));
}
```

---

## Problema 2: Duplicación de Response Building

### Código Actual ❌

**En RegisterHandler (líneas 83-94):**
```csharp
var token = _jwtTokenGenerator.GenerateToken(user);
var roles = user.Roles.Select(role => role.RoleCode).ToArray();
var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(permission => permission).ToArray();

return Result<RegisterResponse>.Success(
    new RegisterResponse(
        user.Id.Value,
        user.Username.Value,
        user.Email.Value,
        token,
        roles,
        permissions));
```

**En LoginHandler (idéntico):**
```csharp
var token = _jwtTokenGenerator.GenerateToken(user);
var roles = user.Roles.Select(role => role.RoleCode).ToArray();
var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(permission => permission).ToArray();

return Result<LoginResponse>.Success(
    new LoginResponse(
        user.Id.Value,
        user.Username.Value,
        user.Email.Value,
        token,
        roles,
        permissions));
```

### Código Mejorado ✅

**Paso 1: Crear mapper base**

Crear archivo: `eiti.Application/Features/Auth/Common/AuthenticationMapper.cs`

```csharp
using eiti.Application.Common.Authorization;
using eiti.Domain.Users;

namespace eiti.Application.Features.Auth.Common;

public static class AuthenticationMapper
{
    public static (string[] Roles, string[] Permissions) MapRolesAndPermissions(User user)
    {
        var roles = user.Roles.Select(role => role.RoleCode).ToArray();
        var permissions = RoleCatalog.PermissionsFor(roles).OrderBy(p => p).ToArray();
        return (roles, permissions);
    }
}
```

**Paso 2: Actualizar RegisterHandler**

```csharp
using eiti.Application.Features.Auth.Common;  // ✅ Agregar

public async Task<Result<RegisterResponse>> Handle(
    RegisterCommand request,
    CancellationToken cancellationToken)
{
    // ... validación y creación de usuario

    var token = _jwtTokenGenerator.GenerateToken(user);
    var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

    return Result<RegisterResponse>.Success(
        new RegisterResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            token,
            roles,
            permissions));
}
```

**Paso 3: Actualizar LoginHandler**

```csharp
using eiti.Application.Features.Auth.Common;  // ✅ Agregar

public async Task<Result<LoginResponse>> Handle(
    LoginQuery request,
    CancellationToken cancellationToken)
{
    // ... autenticación y validación

    var token = _jwtTokenGenerator.GenerateToken(user);
    var (roles, permissions) = AuthenticationMapper.MapRolesAndPermissions(user);

    return Result<LoginResponse>.Success(
        new LoginResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            token,
            roles,
            permissions));
}
```

---

## Problema 3: Duplicación de Authorization Checks

### Código Actual ❌

**CloseCashSessionHandler (línea 29-32):**
```csharp
if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
{
    return Result<CashSessionResponse>.Failure(
        Error.Unauthorized("CashSessions.Close.Unauthorized", "The current user is not authenticated."));
}
```

**OpenCashSessionHandler (línea 32-35):**
```csharp
if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
{
    return Result<CashSessionResponse>.Failure(
        Error.Unauthorized("CashSessions.Open.Unauthorized", "The current user is not authenticated."));
}
```

**CreateSaleHandler (línea 53-58):**
```csharp
if (!_currentUserService.IsAuthenticated || companyId is null)
{
    return Result<CreateSaleResponse>.Failure(
        Error.Unauthorized("Sales.Create.Unauthorized", "The current user is not authenticated."));
}
```

### Código Mejorado ✅

**Crear archivo: `eiti.Application/Abstractions/Services/CurrentUserServiceExtensions.cs`**

```csharp
using eiti.Application.Common;

namespace eiti.Application.Abstractions.Services;

public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// Verifica que el usuario esté autenticado
    /// </summary>
    public static Result EnsureAuthenticated(this ICurrentUserService service)
    {
        if (!service.IsAuthenticated)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.NotAuthenticated",
                    "The current user is not authenticated."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Verifica que el usuario esté autenticado y tenga contexto (companyId y userId)
    /// </summary>
    public static Result EnsureAuthenticatedWithContext(this ICurrentUserService service)
    {
        if (!service.IsAuthenticated)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.NotAuthenticated",
                    "The current user is not authenticated."));
        }

        if (service.CompanyId is null || service.UserId is null)
        {
            return Result.Failure(
                Error.Unauthorized(
                    "Auth.IncompleteContext",
                    "The current user context is incomplete."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Verifica que el usuario tenga una permisión específica
    /// </summary>
    public static Result EnsureHasPermission(
        this ICurrentUserService service,
        string permission,
        string errorCode = "Auth.Forbidden",
        string errorMessage = "The current user does not have the required permission.")
    {
        if (!service.HasPermission(permission))
        {
            return Result.Failure(
                Error.Forbidden(errorCode, errorMessage));
        }

        return Result.Success();
    }
}
```

**Usar en CloseCashSessionHandler:**

```csharp
public async Task<Result<CashSessionResponse>> Handle(
    CloseCashSessionCommand request,
    CancellationToken cancellationToken)
{
    // ✅ Una línea reemplaza 3 líneas de validación
    var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
    if (authCheck.IsFailure)
        return Result<CashSessionResponse>.Failure(authCheck.Error);

    var session = await _cashSessionRepository.GetByIdAsync(
        new CashSessionId(request.Id),
        _currentUserService.CompanyId!,
        cancellationToken);

    if (session is null)
    {
        return Result<CashSessionResponse>.Failure(
            Error.NotFound("CashSessions.Close.NotFound", "The requested cash session was not found."));
    }

    // ... resto del código
}
```

**Usar en CreateSaleHandler:**

```csharp
public async Task<Result<CreateSaleResponse>> Handle(
    CreateSaleCommand request,
    CancellationToken cancellationToken)
{
    var authCheck = _currentUserService.EnsureAuthenticated();
    if (authCheck.IsFailure)
        return Result<CreateSaleResponse>.Failure(authCheck.Error);

    var companyId = _currentUserService.CompanyId;
    if (companyId is null)
    {
        return Result<CreateSaleResponse>.Failure(
            Error.Unauthorized("Sales.Create.NoCompany", "User company context not found."));
    }

    // ... resto del código
}
```

---

## Problema 4: Complejidad en CreateSaleHandler

### Código Actual ❌

**Archivo:** `eiti.Application/Features/Sales/Commands/CreateSale/CreateSaleHandler.cs` (431 líneas)

El método `Handle()` hace demasiadas cosas:
1. Validar autorización (líneas 52-78) - 6 checks
2. Validar branch (líneas 80-85)
3. Validar cliente (líneas 87-96)
4. Procesar detalles (líneas 98-140)
5. Procesar pagos (líneas 142-...)
6. Procesar trade-ins (líneas ...-...)
7. Crear venta
8. Procesar stock movements

### Código Mejorado ✅

**Refactorizar en métodos privados:**

```csharp
public sealed class CreateSaleHandler : IRequestHandler<CreateSaleCommand, Result<CreateSaleResponse>>
{
    // ... dependencies igual

    public async Task<Result<CreateSaleResponse>> Handle(
        CreateSaleCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUserService.CompanyId;
        if (!_currentUserService.IsAuthenticated || companyId is null)
        {
            return Result<CreateSaleResponse>.Failure(
                Error.Unauthorized("Sales.Create.Unauthorized", "The current user is not authenticated."));
        }

        // ✅ Extraer cada paso lógico a método privado
        var requestValidation = ValidateCreateSaleRequest(request);
        if (requestValidation.IsFailure)
            return Result<CreateSaleResponse>.Failure(requestValidation.Error);

        var branch = await ValidateAndGetBranchAsync(request.BranchId, companyId, cancellationToken);
        if (branch is null)
            return Result<CreateSaleResponse>.Failure(
                Error.NotFound("Sales.Create.BranchNotFound", "The requested branch was not found."));

        var customer = await GetCustomerIfProvidedAsync(request.CustomerId, companyId, cancellationToken);

        var detailsValidation = ValidateAndGroupSaleDetails(request.Details);
        if (detailsValidation.IsFailure)
            return Result<CreateSaleResponse>.Failure(detailsValidation.Error);

        // ... más métodos privados para payments y trade-ins

        // Crear venta
        var sale = Sale.Create(
            companyId,
            branch.Id,
            customer?.Id,
            request.IsDelivery,
            requestedStatus,
            // ... detalles
        );

        await _saleRepository.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateSaleResponse>.Success(SaleResponseMapper.Map(sale));
    }

    private Result ValidateCreateSaleRequest(CreateSaleCommand request)
    {
        if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        {
            return Result.Failure(
                Error.Validation("Sales.Create.InvalidStatus", "The selected sale status is invalid."));
        }

        var requestedStatus = (SaleStatus)request.IdSaleStatus;

        if (requestedStatus == SaleStatus.Paid && !_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result.Failure(
                Error.Forbidden("Sales.Create.PaymentForbidden", "The current user does not have permission to charge sales."));
        }

        if (requestedStatus == SaleStatus.Cancel)
        {
            return Result.Failure(
                Error.Validation("Sales.Create.CancelNotAllowed", "A sale cannot be created with Cancel status."));
        }

        return Result.Success();
    }

    private async Task<Branch?> ValidateAndGetBranchAsync(Guid branchId, CompanyId companyId, CancellationToken cancellationToken)
    {
        return await _branchRepository.GetByIdAsync(new BranchId(branchId), companyId, cancellationToken);
    }

    private async Task<Customer?> GetCustomerIfProvidedAsync(Guid? customerId, CompanyId companyId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
            return null;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(customerId.Value), companyId, cancellationToken);
        return customer;
    }

    private Result ValidateAndGroupSaleDetails(CreateSaleDetailItemRequest[] details)
    {
        if (details is null || details.Length == 0)
        {
            return Result.Failure(
                Error.Validation("Sales.Create.NoDetails", "Sale must have at least one detail."));
        }

        // ... validación de detalles
        return Result.Success();
    }
}
```

---

## Problema 5: Duplicación en Error Codes

### Código Actual ❌

**Errores inline en CreateSaleHandler:**
```csharp
Error.Unauthorized("Sales.Create.Unauthorized", "...")
Error.Validation("Sales.Create.InvalidStatus", "...")
Error.Forbidden("Sales.Create.PaymentForbidden", "...")
Error.Validation("Sales.Create.CancelNotAllowed", "...")
Error.NotFound("Sales.Create.BranchNotFound", "...")
Error.NotFound("Sales.Create.CustomerNotFound", "...")
```

### Código Mejorado ✅

**Crear archivo: `eiti.Application/Features/Sales/Commands/Common/CreateSaleErrors.cs`**

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.Common;

public static class CreateSaleErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.Create.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Sales.Create.InvalidStatus",
        "The selected sale status is invalid.");

    public static readonly Error PaymentForbidden = Error.Forbidden(
        "Sales.Create.PaymentForbidden",
        "The current user does not have permission to charge sales.");

    public static readonly Error CancelNotAllowed = Error.Validation(
        "Sales.Create.CancelNotAllowed",
        "A sale cannot be created with Cancel status.");

    public static readonly Error BranchNotFound = Error.NotFound(
        "Sales.Create.BranchNotFound",
        "The requested branch was not found.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.Create.CustomerNotFound",
        "The selected customer was not found.");

    public static readonly Error NoDetails = Error.Validation(
        "Sales.Create.NoDetails",
        "Sale must have at least one detail.");

    public static readonly Error InsufficientStock = Error.Conflict(
        "Sales.Create.InsufficientStock",
        "Insufficient stock for product.");
}
```

**Usar en Handler:**

```csharp
if (requestValidation.IsFailure)
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.InvalidStatus);

if (branch is null)
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.BranchNotFound);

if (customer is null)
    return Result<CreateSaleResponse>.Failure(CreateSaleErrors.CustomerNotFound);
```

---

## Problema 6: Mejora en ResultExtensions

### Código Actual ❌

**Archivo:** `eiti.Api/Extensions/ResultExtensions.cs`

Líneas 25-33 y 47-58 tienen duplicación:

```csharp
private static IActionResult ToProblemDetails(Result result)
{
    var statusCode = result.Error.Type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };

    return new ObjectResult(new ProblemDetails
    {
        Status = statusCode,
        Title = GetTitle(result.Error.Type),  // ⚠️ Duplicación aquí
        Detail = result.Error.Description,
        Extensions = { ["errorCode"] = result.Error.Code }
    })
    {
        StatusCode = statusCode
    };
}

private static string GetTitle(ErrorType errorType)
{
    return errorType switch
    {
        ErrorType.Validation => "Validation Error",
        ErrorType.NotFound => "Not Found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        _ => "Internal Server Error"
    };
}
```

### Código Mejorado ✅

```csharp
using eiti.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? new OkObjectResult(result.Value)
            : ToProblemDetails(result);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        return result.IsSuccess
            ? new NoContentResult()
            : ToProblemDetails(result);
    }

    private static IActionResult ToProblemDetails(Result result)
    {
        var (statusCode, title) = GetErrorMapping(result.Error.Type);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = result.Error.Description,
            Extensions = new Dictionary<string, object?>
            {
                ["errorCode"] = result.Error.Code
            }
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Mapea ErrorType a (HttpStatusCode, ProblemTitle)
    /// Centraliza la lógica de conversión de errores de negocio a HTTP
    /// </summary>
    private static (int StatusCode, string Title) GetErrorMapping(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => (
                StatusCodes.Status400BadRequest,
                "Validation Error"),

            ErrorType.NotFound => (
                StatusCodes.Status404NotFound,
                "Not Found"),

            ErrorType.Conflict => (
                StatusCodes.Status409Conflict,
                "Conflict"),

            ErrorType.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized"),

            ErrorType.Forbidden => (
                StatusCodes.Status403Forbidden,
                "Forbidden"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error")
        };
    }
}
```

---

## Impacto Estimado

| Refactoring | Complejidad Reducida | Líneas Eliminadas | Tests Ganados |
|-------------|---------------------|------------------|---------------|
| LoginHandler Fix | -3 CC | ~15 | +1 |
| Auth Response Mapper | -2 CC | ~30 | 0 |
| Auth Checks Extension | -4 CC | ~50 | +2 |
| CreateSaleHandler Split | -8 CC | ~150 | +3 |
| Error Classes | 0 CC | -20 (suma) | +2 |
| ResultExtensions | -1 CC | ~25 | 0 |
| **TOTAL** | **-18 CC** | **~240 líneas** | **+8 tests** |

**Resultado Final:**
- Complejidad de 8.5 → 5.2 (MÁS MANTENIBLE)
- Cobertura 4.9 → 6.5 (MEJOR)
- Código duplicado: 3 → 1.5

---

**Tiempo Estimado de Implementación:**
- Phase 1: 4-6 horas
- Phase 2: 8-12 horas
- Phase 3: 4-6 horas
- **Total: 16-24 horas de trabajo**
