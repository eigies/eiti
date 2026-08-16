# Rediseño del Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar el cálculo del dashboard en el browser por un endpoint agregado, y reordenar la pantalla para priorizar la lectura del mes en curso cortada por minorista / cuenta corriente, con filtro global de sucursal.

**Architecture:** Nueva feature vertical slice `Features/Dashboard/Queries/GetDashboardSummary/` que devuelve ~6 KB constantes en vez del mes entero de ventas (~1 MB hoy). El front pasa de derivar ~35 getters a consumir un contrato ya calculado. Se agrega compresión de respuestas a la API, transversal a toda la app.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core / Npgsql, xUnit + Moq + FluentAssertions. Angular 16 standalone, RxJS 7, CSS con custom properties.

**Spec:** `docs/superpowers/specs/2026-08-15-dashboard-rediseno-design.md`

## Global Constraints

- **Backend vertical slice:** un archivo por artefacto en `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/` — `GetDashboardSummaryQuery.cs`, `GetDashboardSummaryHandler.cs`, `GetDashboardSummaryResponse.cs`, `GetDashboardSummaryValidator.cs`, `GetDashboardSummaryErrors.cs`.
- **Todo handler arranca con** `var authCheck = _currentUserService.EnsureAuthenticated(); if (authCheck.IsFailure) return Result<T>.Failure(authCheck.Error);`
- **Nunca `throw` para errores de negocio** — `Result.Failure(Error)` con constantes en el archivo `*Errors.cs`.
- **La query NO implementa `IRequirePermissions`** — alcanza con estar autenticado, igual que hoy. Confirmado en el spec: hay perfiles "Cajero" sin `sales.access`.
- **Fechas siempre por `BusinessCalendar`** (`eiti.Application/Common/BusinessCalendar.cs`): `ToUtcRange`, `StartOfDayUtc`, `EndOfDayUtc`. Nunca `.Date` crudo contra columnas UTC.
- **No se crea ningún permiso nuevo.** No tocar `PermissionCodes`, `PermissionCatalog`, `RoleCatalog` ni `permission.models.ts`.
- **Build backend siempre con dependencias:** `dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`. Nunca `--no-dependencies`.
- **Build front obligatorio antes de dar por terminada cualquier tarea de front:** `cd C:/EiTeFront/eiti-front && ng build --configuration development`.
- **Front:** standalone components, sin NgModules. Servicios HTTP en `core/services/` con `providedIn: 'root'` y URL desde `environment.apiUrl`. Modelos en `core/models/<domain>.models.ts`. TypeScript strict, sin `any`.
- **CSS:** solo custom properties existentes (`--amber`, `--success`, `--danger`, `--text`, `--bg-panel`, `--border-2`, …). Cero colores hardcodeados. Verificar en `theme-dark` y `theme-light`.
- **Series del gráfico:** `--amber` para minorista, `--success` para cuenta corriente, con leyenda textual.
- **Commits:** en `develop`, terminando con `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`. No deployar hasta la última tarea.

---

### Task 1: Compresión de respuestas en la API

Independiente del dashboard: beneficia toda la app y se puede revisar por separado.

**Files:**
- Modify: `eiti.Api/Program.cs:14` (usings), `:129` (servicios), `:144` (pipeline)

**Interfaces:**
- Consumes: nada.
- Produces: nada que el código consuma. Efecto observable: header `content-encoding: br|gzip` en respuestas JSON.

- [ ] **Step 1: Agregar el using**

En `eiti.Api/Program.cs`, junto a los demás usings del bloque inicial:

```csharp
using Microsoft.AspNetCore.ResponseCompression;
```

- [ ] **Step 2: Registrar el servicio antes de `builder.Build()`**

Insertar antes de la línea `var app = builder.Build();`:

```csharp
// El JSON de la app comprime ~85%. El dashboard y los reportes eran los mas pesados.
// Brotli primero (mejor ratio), gzip como fallback para clientes que no lo soporten.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
```

- [ ] **Step 3: Insertar en el pipeline**

En `eiti.Api/Program.cs`, la compresión va **antes** de todo lo que escribe respuestas. Insertar inmediatamente después de `var app = builder.Build();` y antes de `app.UseMiddleware<GlobalExceptionHandlingMiddleware>();`:

```csharp
app.UseResponseCompression();
```

- [ ] **Step 4: Compilar**

Run: `cd C:/Eiti/eiti && dotnet build eiti.Api/eiti.Api.csproj`
Expected: `0 Errores`

- [ ] **Step 5: Verificar que la suite sigue verde**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: `Correctas!` con 209 o más tests pasando, 0 con error.

Nota: la compresión no se puede verificar con un unit test — se verifica en la Task 10 contra producción, con `curl -H "Accept-Encoding: br"` mirando el header `content-encoding`.

- [ ] **Step 6: Commit**

```bash
cd C:/Eiti/eiti && git checkout develop
git add eiti.Api/Program.cs
git commit -m "perf(api): comprimir respuestas con brotli y gzip

El JSON de la app viajaba sin comprimir. Los payloads mas pesados (dashboard,
listado de ventas, reportes) bajan alrededor de 85%.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Contrato del endpoint — Query, Response, Errors y Validator

**Files:**
- Create: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryQuery.cs`
- Create: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryResponse.cs`
- Create: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryErrors.cs`
- Create: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryValidator.cs`
- Test: `eiti.Tests/GetDashboardSummaryValidatorTests.cs`

**Interfaces:**
- Consumes: `eiti.Application.Common.Result`, `eiti.Application.Common.Error`.
- Produces: `GetDashboardSummaryQuery(DateTime DateFrom, DateTime DateTo, Guid? BranchId)`; `GetDashboardSummaryResponse(DashboardPeriodTotals Month, DashboardPeriodTotals Today, IReadOnlyList<DashboardDayPoint> Days, IReadOnlyList<DashboardTopProduct> TopProducts, DashboardCollections Collections, DashboardTodayStatus TodayStatus, IReadOnlyList<DashboardRecentSale> RecentSales)`; `GetDashboardSummaryErrors.BranchNotAllowed`.

- [ ] **Step 1: Crear la Query**

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryQuery.cs`:

```csharp
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

// Agregados del dashboard inicial. DateFrom/DateTo llegan como fecha local del usuario
// (el mes en curso) y se traducen a UTC con BusinessCalendar en el handler.
// BranchId null = todas las sucursales que el usuario tenga permitidas.
//
// No implementa IRequirePermissions a proposito: la ruta /dashboard hoy solo exige estar
// autenticado, y hay perfiles ("Cajero") sin sales.access que deben poder entrar igual.
// Lo sensible son los importes, y eso lo gatea DashboardViewFinancials dentro del handler.
public sealed record GetDashboardSummaryQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null
) : IRequest<Result<GetDashboardSummaryResponse>>;
```

- [ ] **Step 2: Crear el Response**

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryResponse.cs`:

```csharp
namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryResponse(
    DashboardPeriodTotals Month,
    DashboardPeriodTotals Today,
    IReadOnlyList<DashboardDayPoint> Days,
    IReadOnlyList<DashboardTopProduct> TopProducts,
    DashboardCollections Collections,
    DashboardTodayStatus TodayStatus,
    IReadOnlyList<DashboardRecentSale> RecentSales);

// Minorista = venta normal; CuentaCorriente = IsCuentaCorriente. Misma definicion que
// SalesReport y WholesaleByCustomer. Las canceladas quedan afuera de los tres segmentos.
public sealed record DashboardPeriodTotals(
    DashboardSegment Total,
    DashboardSegment Retail,
    DashboardSegment CurrentAccount);

// Amount = facturacion de las ventas activas, NO lo cobrado. Lo cobrado vive en Collections.
public sealed record DashboardSegment(int Count, decimal Amount);

public sealed record DashboardDayPoint(
    DateTime Date,
    int RetailCount,
    decimal RetailAmount,
    int CurrentAccountCount,
    decimal CurrentAccountAmount);

public sealed record DashboardTopProduct(
    Guid ProductId,
    string Name,
    string Brand,
    int Units,
    int SalesCount);

public sealed record DashboardCollections(
    decimal PaidAmount,
    int PaidCount,
    decimal PendingAmount,
    int PendingCount,
    decimal AvgTicket);

public sealed record DashboardTodayStatus(
    int ActiveCount,
    int PaidCount,
    int PendingCount,
    int CancelledCount);

public sealed record DashboardRecentSale(
    Guid Id,
    string? Code,
    DateTime CreatedAt,
    string CustomerName,
    int SaleStatus,
    decimal TotalAmount,
    bool IsCuentaCorriente);
```

- [ ] **Step 3: Crear los Errors**

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryErrors.cs`:

```csharp
using eiti.Application.Common;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

internal static class GetDashboardSummaryErrors
{
    public static readonly Error BranchNotAllowed = Error.Forbidden(
        "Dashboard.Summary.BranchNotAllowed",
        "No tenes acceso a la sucursal solicitada.");
}
```

`Error.Forbidden(string code, string description)` existe en `eiti.Application/Common/Error.cs:33` — verificado.

- [ ] **Step 4: Escribir los tests del validator (fallan)**

`eiti.Tests/GetDashboardSummaryValidatorTests.cs`:

```csharp
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using FluentAssertions;

namespace eiti.Tests;

public sealed class GetDashboardSummaryValidatorTests
{
    private readonly GetDashboardSummaryValidator _validator = new();

    [Fact]
    public void RangoValido_Pasa()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DesdePosteriorAHasta_Falla()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            new DateTime(2026, 8, 31), new DateTime(2026, 8, 1)));

        result.IsValid.Should().BeFalse();
        // "posterior" es la redaccion canonica del repo: 6 usos, todos los validators de reportes.
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("posterior"));
    }

    [Fact]
    public void FechaDesdeVacia_Falla()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            default, new DateTime(2026, 8, 31)));

        result.IsValid.Should().BeFalse();
    }
}
```

- [ ] **Step 5: Correr los tests para verificar que fallan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryValidator"`
Expected: error de compilación — `GetDashboardSummaryValidator` no existe todavía.

- [ ] **Step 6: Crear el Validator**

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryValidator.cs`:

```csharp
using FluentValidation;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryValidator : AbstractValidator<GetDashboardSummaryQuery>
{
    public GetDashboardSummaryValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEqual(default(DateTime)).WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.DateTo)
            .NotEqual(default(DateTime)).WithMessage("La fecha hasta es obligatoria.");
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom != default && x.DateTo != default)
            .WithMessage("La fecha desde no puede ser posterior a la fecha hasta.");
    }
}
```

- [ ] **Step 7: Correr los tests para verificar que pasan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryValidator"`
Expected: `Correctas!` — 3 tests pasando.

- [ ] **Step 8: Commit**

```bash
cd C:/Eiti/eiti
git add eiti.Application/Features/Dashboard eiti.Tests/GetDashboardSummaryValidatorTests.cs
git commit -m "feat(dashboard): contrato del endpoint de resumen

Query, response, errors y validator de GetDashboardSummary. El contrato devuelve
agregados ya calculados (~6 KB constantes) en vez del mes entero de ventas.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Handler — sucursal, segmentación del mes y de hoy

**Files:**
- Create: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryHandler.cs`
- Test: `eiti.Tests/GetDashboardSummaryHandlerTests.cs`

**Interfaces:**
- Consumes: `GetDashboardSummaryQuery`, `GetDashboardSummaryResponse`, `GetDashboardSummaryErrors`, `ICurrentUserService`, `ISaleRepository.ListForSalesReportAsync(CompanyId, DateTime from, DateTime to, Guid? branchId, Guid? customerId, IReadOnlyCollection<Guid>? allowedBranchIds, CancellationToken)`, `BusinessCalendar`.
- Produces: `GetDashboardSummaryHandler` con constructor `(ICurrentUserService, ISaleRepository, IProductRepository, ICustomerRepository)`.

- [ ] **Step 1: Escribir los tests que fallan**

`eiti.Tests/GetDashboardSummaryHandlerTests.cs`:

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class GetDashboardSummaryHandlerTests
{
    private static readonly CompanyId Company = CompanyId.New();
    private static readonly BranchId BranchA = BranchId.New();
    private static readonly BranchId BranchB = BranchId.New();

    private static Mock<ICurrentUserService> MockUser(
        bool canViewAll = true,
        bool canViewFinancials = true,
        IReadOnlyCollection<Guid>? allowedBranches = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.CompanyId).Returns(Company);
        user.SetupGet(u => u.CanViewAllBranches).Returns(canViewAll);
        user.SetupGet(u => u.AllowedBranchIds).Returns(allowedBranches ?? Array.Empty<Guid>());
        user.Setup(u => u.HasPermission(PermissionCodes.DashboardViewFinancials))
            .Returns(canViewFinancials);
        return user;
    }

    private static Product SampleProduct() =>
        Product.Create(Company, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 100_000m, 70_000m, null);

    private static Sale RetailSale(BranchId branchId, ProductId productId, decimal price) =>
        Sale.Create(Company, branchId, null, false, SaleStatus.Paid,
            [SaleDetail.Create(productId, 1, price)],
            [SalePayment.Create(SalePaymentMethod.Cash, price, null)],
            allowOverpayment: true);

    private static Sale CcSale(BranchId branchId, CustomerId customerId, ProductId productId, decimal price) =>
        Sale.CreateCc(Company, branchId, customerId, [SaleDetail.Create(productId, 1, price)]);

    private static GetDashboardSummaryHandler BuildHandler(
        Mock<ICurrentUserService> user,
        IReadOnlyList<Sale> sales,
        Product product)
    {
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByCompanyIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var customerRepository = new Mock<ICustomerRepository>();

        return new GetDashboardSummaryHandler(
            user.Object, saleRepository.Object, productRepository.Object, customerRepository.Object);
    }

    [Fact]
    public async Task SepararMinoristaDeCuentaCorriente()
    {
        var product = SampleProduct();
        var customer = CustomerId.New();
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            RetailSale(BranchA, product.Id, 50_000m),
            CcSale(BranchA, customer, product.Id, 30_000m)
        };
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Total.Count.Should().Be(3);
        result.Value.Month.Total.Amount.Should().Be(180_000m);
        result.Value.Month.Retail.Count.Should().Be(2);
        result.Value.Month.Retail.Amount.Should().Be(150_000m);
        result.Value.Month.CurrentAccount.Count.Should().Be(1);
        result.Value.Month.CurrentAccount.Amount.Should().Be(30_000m);
    }

    [Fact]
    public async Task SucursalAjena_SinPermisoDeVerTodas_EsRechazada()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]),
            [],
            product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), BranchB.Value),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.Summary.BranchNotAllowed");
    }

    [Fact]
    public async Task SucursalPropia_SinPermisoDeVerTodas_EsAceptada()
    {
        var product = SampleProduct();
        var handler = BuildHandler(
            MockUser(canViewAll: false, allowedBranches: [BranchA.Value]),
            [RetailSale(BranchA, product.Id, 100_000m)],
            product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), BranchA.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Total.Count.Should().Be(1);
    }

    [Fact]
    public async Task VentasDeHoy_QuedanEnLaColumnaHoy()
    {
        var product = SampleProduct();
        // Sale.Create pone CreatedAt = UtcNow, o sea que las ventas del set son "de hoy".
        var handler = BuildHandler(MockUser(), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Today.Total.Count.Should().Be(1);
        result.Value.Today.Retail.Count.Should().Be(1);
        result.Value.Today.CurrentAccount.Count.Should().Be(0);
    }
}
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryHandler"`
Expected: error de compilación — `GetDashboardSummaryHandler` no existe.

- [ ] **Step 3: Escribir el handler**

`eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryHandler.cs`:

```csharp
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, Result<GetDashboardSummaryResponse>>
{
    private const int RecentSalesCount = 6;
    private const int TopProductsCount = 5;
    private const int ChartDays = 7;

    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;

    public GetDashboardSummaryHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<GetDashboardSummaryResponse>> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetDashboardSummaryResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var canViewAllBranches = _currentUserService.CanViewAllBranches;
        var allowedBranchIds = canViewAllBranches ? null : _currentUserService.AllowedBranchIds;

        // Que el selector no ofrezca una sucursal no alcanza: se valida en el server.
        if (request.BranchId.HasValue
            && !canViewAllBranches
            && !_currentUserService.AllowedBranchIds.Contains(request.BranchId.Value))
        {
            return Result<GetDashboardSummaryResponse>.Failure(GetDashboardSummaryErrors.BranchNotAllowed);
        }

        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var sales = await _saleRepository.ListForSalesReportAsync(
            companyId, from, to, request.BranchId, null, allowedBranchIds, cancellationToken);

        // ListForSalesReportAsync ya excluye canceladas, pero el dashboard necesita contarlas
        // para el Pulso del dia, asi que se recalcula sobre lo que hay.
        var month = BuildTotals(sales);

        var todayLocal = TodayLocal();
        var todayFrom = BusinessCalendar.StartOfDayUtc(todayLocal);
        var todayTo = BusinessCalendar.EndOfDayUtc(todayLocal);
        var todaySales = sales.Where(s => s.CreatedAt >= todayFrom && s.CreatedAt <= todayTo).ToList();
        var today = BuildTotals(todaySales);

        return Result<GetDashboardSummaryResponse>.Success(new GetDashboardSummaryResponse(
            month,
            today,
            Array.Empty<DashboardDayPoint>(),
            Array.Empty<DashboardTopProduct>(),
            new DashboardCollections(0m, 0, 0m, 0, 0m),
            new DashboardTodayStatus(todaySales.Count, 0, 0, 0),
            Array.Empty<DashboardRecentSale>()));
    }

    // El "hoy" del usuario, no el del servidor: se toma la fecha local segun BusinessCalendar.
    private static DateTime TodayLocal() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessCalendar.TimeZone).Date;

    private static DashboardPeriodTotals BuildTotals(IReadOnlyCollection<Sale> sales)
    {
        var retail = sales.Where(s => !s.IsCuentaCorriente).ToList();
        var currentAccount = sales.Where(s => s.IsCuentaCorriente).ToList();

        return new DashboardPeriodTotals(
            Segment(sales),
            Segment(retail),
            Segment(currentAccount));
    }

    private static DashboardSegment Segment(IReadOnlyCollection<Sale> sales) =>
        new(sales.Count, decimal.Round(sales.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero));
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryHandler"`
Expected: `Correctas!` — 4 tests pasando.

Si `SucursalAjena...` falla porque `Error.Code` tiene otro nombre de propiedad, inspeccionar `eiti.Application/Common/Error.cs` y ajustar la aserción.

- [ ] **Step 5: Compilar todo**

Run: `cd C:/Eiti/eiti && dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: `0 Errores`

- [ ] **Step 6: Commit**

```bash
cd C:/Eiti/eiti
git add eiti.Application/Features/Dashboard eiti.Tests/GetDashboardSummaryHandlerTests.cs
git commit -m "feat(dashboard): handler con segmentacion minorista/CC y validacion de sucursal

Totales del mes y del dia local separados en Total, Minorista y Cuenta Corriente.
El dia local se resuelve con BusinessCalendar, no con la fecha UTC del servidor.
Pedir una sucursal ajena sin branches.view_all devuelve BranchNotAllowed.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Handler — serie de 7 días, top de productos, cobranza, pulso y últimas ventas

**Files:**
- Modify: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryHandler.cs`
- Modify: `eiti.Application/Abstractions/Repositories/ISaleRepository.cs`
- Modify: `eiti.Infrastructure/Persistence/Repositories/SaleRepository.cs`
- Modify: `eiti.Tests/GetDashboardSummaryHandlerTests.cs`

**Interfaces:**
- Consumes: lo de la Task 3, más `IProductRepository.GetByCompanyIdAsync(CompanyId, CancellationToken)` y `ICustomerRepository.ListByIdsAsync(CompanyId, IEnumerable<CustomerId>, CancellationToken)`.
- Produces: el `GetDashboardSummaryResponse` completo, sin campos vacíos, y `ISaleRepository.CountCancelledAsync(CompanyId, DateTime from, DateTime to, Guid? branchId, IReadOnlyCollection<Guid>? allowedBranchIds, CancellationToken)`.

- [ ] **Step 1: Agregar los tests que fallan**

Agregar a `eiti.Tests/GetDashboardSummaryHandlerTests.cs`, dentro de la clase:

```csharp
    [Fact]
    public async Task SerieDeSieteDias_SiempreTieneSieteDias_YLosVaciosEnCero()
    {
        var product = SampleProduct();
        var handler = BuildHandler(MockUser(), [RetailSale(BranchA, product.Id, 100_000m)], product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Days.Should().HaveCount(7);
        result.Value.Days.Should().BeInAscendingOrder(d => d.Date);
        // La venta es de hoy, asi que el ultimo punto la tiene y los anteriores estan en cero.
        result.Value.Days[^1].RetailCount.Should().Be(1);
        result.Value.Days[0].RetailCount.Should().Be(0);
        result.Value.Days[0].RetailAmount.Should().Be(0m);
    }

    [Fact]
    public async Task TopProductos_OrdenaPorUnidades()
    {
        var product = SampleProduct();
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            RetailSale(BranchA, product.Id, 100_000m)
        };
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.TopProducts.Should().HaveCount(1);
        result.Value.TopProducts[0].ProductId.Should().Be(product.Id.Value);
        result.Value.TopProducts[0].Units.Should().Be(2);
        result.Value.TopProducts[0].SalesCount.Should().Be(2);
        result.Value.TopProducts[0].Name.Should().Be("Bateria");
    }

    [Fact]
    public async Task Cobranza_SeCalculaPorEstadoNoPorPagos()
    {
        var product = SampleProduct();
        // Sale.Create con SaleStatus.Paid => cobrada. Una CC nace OnHold => pendiente.
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            CcSale(BranchA, CustomerId.New(), product.Id, 40_000m)
        };
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.Collections.PaidAmount.Should().Be(100_000m);
        result.Value.Collections.PaidCount.Should().Be(1);
        result.Value.Collections.PendingAmount.Should().Be(40_000m);
        result.Value.Collections.PendingCount.Should().Be(1);
        result.Value.Collections.AvgTicket.Should().Be(70_000m);
    }

    [Fact]
    public async Task UltimasVentas_DevuelveComoMaximoSeisYLasMasRecientesPrimero()
    {
        var product = SampleProduct();
        var sales = Enumerable.Range(0, 8)
            .Select(_ => RetailSale(BranchA, product.Id, 10_000m))
            .ToList();
        var handler = BuildHandler(MockUser(), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.RecentSales.Should().HaveCount(6);
        result.Value.RecentSales.Should().BeInDescendingOrder(s => s.CreatedAt);
    }
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryHandler"`
Expected: los 4 nuevos FALLAN (`Days` vacío, `TopProducts` vacío, `Collections` en cero, `RecentSales` vacío). Los 4 de la Task 3 siguen pasando.

- [ ] **Step 2b: Agregar `CountCancelledAsync` al repositorio**

`ListForSalesReportAsync` filtra `SaleStatus != Cancel`, así que las canceladas nunca llegan al handler. El Pulso del día las necesita, y una consulta `COUNT` es más barata que traerlas.

En `eiti.Application/Abstractions/Repositories/ISaleRepository.cs`, junto a `ListForSalesReportAsync`:

```csharp
    // Cantidad de ventas canceladas del período. El dashboard la necesita para el pulso del
    // día, y ListForSalesReportAsync las excluye por diseño. Un COUNT evita traerlas.
    Task<int> CountCancelledAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);
```

En `eiti.Infrastructure/Persistence/Repositories/SaleRepository.cs`:

```csharp
    public async Task<int> CountCancelledAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .AsNoTracking()
            .Where(sale => sale.CompanyId == companyId
                && sale.CreatedAt >= from
                && sale.CreatedAt <= to
                && sale.SaleStatus == SaleStatus.Cancel);

        if (branchId.HasValue)
        {
            var bId = new BranchId(branchId.Value);
            query = query.Where(sale => sale.BranchId == bId);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            // Comparar el value object entero: acceder a .Value dentro del arbol de expresion
            // no lo traduce EF. Mismo patron que el resto de los filtros por sucursal.
            var allowed = allowedBranchIds.Select(id => new BranchId(id)).ToList();
            query = query.Where(sale => allowed.Contains(sale.BranchId));
        }

        return await query.CountAsync(cancellationToken);
    }
```

Agregar el mock en `BuildHandler` de los tests, para que devuelva 0 por defecto:

```csharp
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
```

Y agregar este test:

```csharp
    [Fact]
    public async Task CanceladasDeHoy_SalenDeLaConsultaAparte()
    {
        var product = SampleProduct();
        var saleRepository = new Mock<ISaleRepository>();
        saleRepository
            .Setup(r => r.ListForSalesReportAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([RetailSale(BranchA, product.Id, 100_000m)]);
        saleRepository
            .Setup(r => r.CountCancelledAsync(
                It.IsAny<CompanyId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByCompanyIdAsync(It.IsAny<CompanyId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var handler = new GetDashboardSummaryHandler(
            MockUser().Object, saleRepository.Object, productRepository.Object,
            new Mock<ICustomerRepository>().Object);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        result.Value.TodayStatus.CancelledCount.Should().Be(3);
        result.Value.TodayStatus.ActiveCount.Should().Be(1);
    }
```

- [ ] **Step 3: Completar el handler**

Reemplazar el `return Result<GetDashboardSummaryResponse>.Success(...)` de la Task 3 por:

```csharp
        var cancelledToday = await _saleRepository.CountCancelledAsync(
            companyId, todayFrom, todayTo, request.BranchId, allowedBranchIds, cancellationToken);

        var days = BuildDays(sales, todayLocal);
        var topProducts = await BuildTopProductsAsync(sales, companyId, cancellationToken);
        var collections = BuildCollections(sales);
        var todayStatus = BuildTodayStatus(todaySales, cancelledToday);
        var recentSales = await BuildRecentSalesAsync(sales, companyId, cancellationToken);

        return Result<GetDashboardSummaryResponse>.Success(new GetDashboardSummaryResponse(
            month, today, days, topProducts, collections, todayStatus, recentSales));
```

Y agregar estos métodos privados a la clase:

```csharp
    // Siempre 7 puntos, del mas viejo al mas nuevo. Los dias sin ventas van en cero,
    // no ausentes: el grafico necesita el eje completo.
    private static IReadOnlyList<DashboardDayPoint> BuildDays(
        IReadOnlyCollection<Sale> sales, DateTime todayLocal)
    {
        var points = new List<DashboardDayPoint>(ChartDays);

        for (var offset = ChartDays - 1; offset >= 0; offset--)
        {
            var day = todayLocal.AddDays(-offset);
            var dayFrom = BusinessCalendar.StartOfDayUtc(day);
            var dayTo = BusinessCalendar.EndOfDayUtc(day);
            var ofDay = sales.Where(s => s.CreatedAt >= dayFrom && s.CreatedAt <= dayTo).ToList();

            var retail = ofDay.Where(s => !s.IsCuentaCorriente).ToList();
            var cc = ofDay.Where(s => s.IsCuentaCorriente).ToList();

            points.Add(new DashboardDayPoint(
                day,
                retail.Count,
                decimal.Round(retail.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
                cc.Count,
                decimal.Round(cc.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero)));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardTopProduct>> BuildTopProductsAsync(
        IReadOnlyCollection<Sale> sales, Domain.Companies.CompanyId companyId, CancellationToken ct)
    {
        var accumulator = new Dictionary<Guid, (int Units, HashSet<Guid> SaleIds)>();

        foreach (var sale in sales)
        {
            foreach (var detail in sale.Details)
            {
                var key = detail.ProductId.Value;
                if (!accumulator.TryGetValue(key, out var acc))
                {
                    acc = (0, new HashSet<Guid>());
                }

                acc.Units += detail.Quantity;
                acc.SaleIds.Add(sale.Id.Value);
                accumulator[key] = acc;
            }
        }

        if (accumulator.Count == 0)
            return Array.Empty<DashboardTopProduct>();

        var products = (await _productRepository.GetByCompanyIdAsync(companyId, ct))
            .ToDictionary(p => p.Id.Value, p => p);

        return accumulator
            .OrderByDescending(kvp => kvp.Value.Units)
            .Take(TopProductsCount)
            .Select(kvp =>
            {
                products.TryGetValue(kvp.Key, out var product);
                return new DashboardTopProduct(
                    kvp.Key,
                    product?.Name ?? "Producto eliminado",
                    product?.Brand ?? "Sin marca",
                    kvp.Value.Units,
                    kvp.Value.SaleIds.Count);
            })
            .ToList();
    }

    // Cobrado y pendiente salen del ESTADO de la venta, no de sus pagos: Sale.MonetaryPaidAmount
    // es _payments.Sum(...) y sin Include(Payments) daria 0. Mismo criterio que el dashboard viejo.
    private static DashboardCollections BuildCollections(IReadOnlyCollection<Sale> sales)
    {
        var paid = sales.Where(s => s.SaleStatus == SaleStatus.Paid).ToList();
        var pending = sales.Where(s => s.SaleStatus == SaleStatus.OnHold).ToList();
        var total = sales.Sum(s => s.TotalAmount);

        return new DashboardCollections(
            decimal.Round(paid.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
            paid.Count,
            decimal.Round(pending.Sum(s => s.TotalAmount), 2, MidpointRounding.AwayFromZero),
            pending.Count,
            sales.Count == 0 ? 0m : decimal.Round(total / sales.Count, 2, MidpointRounding.AwayFromZero));
    }

    // OJO: cancelledCount NO puede salir de todaySales. ListForSalesReportAsync filtra
    // SaleStatus != Cancel, asi que las canceladas nunca llegan y el contador daria siempre 0.
    // Viene de una consulta COUNT aparte (ver Step 4 de esta tarea).
    private static DashboardTodayStatus BuildTodayStatus(
        IReadOnlyCollection<Sale> todaySales, int cancelledCount) =>
        new(todaySales.Count,
            todaySales.Count(s => s.SaleStatus == SaleStatus.Paid),
            todaySales.Count(s => s.SaleStatus == SaleStatus.OnHold),
            cancelledCount);

    private async Task<IReadOnlyList<DashboardRecentSale>> BuildRecentSalesAsync(
        IReadOnlyCollection<Sale> sales, Domain.Companies.CompanyId companyId, CancellationToken ct)
    {
        var recent = sales
            .OrderByDescending(s => s.CreatedAt)
            .Take(RecentSalesCount)
            .ToList();

        if (recent.Count == 0)
            return Array.Empty<DashboardRecentSale>();

        var customerIds = recent
            .Where(s => s.CustomerId is not null)
            .Select(s => s.CustomerId!)
            .Distinct()
            .ToList();

        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds, ct))
                .ToDictionary(c => c.Id.Value, c => c.FullName);

        return recent
            .Select(s => new DashboardRecentSale(
                s.Id.Value,
                s.Code,
                s.CreatedAt,
                s.CustomerId is not null && customerNames.TryGetValue(s.CustomerId.Value, out var name)
                    ? name
                    : "Consumidor final",
                (int)s.SaleStatus,
                s.TotalAmount,
                s.IsCuentaCorriente))
            .ToList();
    }
```

Firma verificada: `ICustomerRepository.ListByIdsAsync(CompanyId companyId, IEnumerable<CustomerId> ids, CancellationToken ct = default)` (`ICustomerRepository.cs:50`). `Sale.CustomerId` ya es un `CustomerId`, así que `customerIds` tipa directo.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~GetDashboardSummaryHandler"`
Expected: `Correctas!` — 9 tests pasando.

- [ ] **Step 4b: Compilar con dependencias**

Run: `cd C:/Eiti/eiti && dotnet build eiti.Infrastructure/eiti.Infrastructure.csproj`
Expected: `0 Errores` — confirma que `CountCancelledAsync` quedó implementado en `SaleRepository` y no solo declarado en la interfaz.

- [ ] **Step 5: Commit**

```bash
cd C:/Eiti/eiti
git add eiti.Application eiti.Infrastructure eiti.Tests/GetDashboardSummaryHandlerTests.cs
git commit -m "feat(dashboard): serie de 7 dias, top de productos, cobranza y ultimas ventas

La serie siempre devuelve 7 puntos con los dias vacios en cero, para que el grafico
tenga el eje completo. Cobranza se calcula por SaleStatus y no por importes de pago,
porque Sale.MonetaryPaidAmount depende de Payments y no se cargan.

Se agrega ISaleRepository.CountCancelledAsync: ListForSalesReportAsync filtra las
canceladas por diseno, asi que el contador del pulso del dia habria quedado siempre
en 0. Un COUNT aparte es mas barato que traerlas.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Handler — la plata no viaja sin permiso

**Files:**
- Modify: `eiti.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryHandler.cs`
- Modify: `eiti.Tests/GetDashboardSummaryHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentUserService.HasPermission(PermissionCodes.DashboardViewFinancials)`.
- Produces: mismo contrato; todos los importes en `0` cuando falta el permiso.

- [ ] **Step 1: Agregar el test que falla**

Agregar a `eiti.Tests/GetDashboardSummaryHandlerTests.cs`:

```csharp
    [Fact]
    public async Task SinPermisoFinanciero_LosImportesSalenEnCero_PeroLasCantidadesNo()
    {
        var product = SampleProduct();
        var sales = new List<Sale>
        {
            RetailSale(BranchA, product.Id, 100_000m),
            CcSale(BranchA, CustomerId.New(), product.Id, 40_000m)
        };
        var handler = BuildHandler(MockUser(canViewFinancials: false), sales, product);

        var result = await handler.Handle(
            new GetDashboardSummaryQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)),
            CancellationToken.None);

        // Las cantidades se mantienen: un vendedor ve su volumen.
        result.Value.Month.Total.Count.Should().Be(2);
        result.Value.Month.Retail.Count.Should().Be(1);

        // Ningun importe viaja.
        result.Value.Month.Total.Amount.Should().Be(0m);
        result.Value.Month.Retail.Amount.Should().Be(0m);
        result.Value.Month.CurrentAccount.Amount.Should().Be(0m);
        result.Value.Today.Total.Amount.Should().Be(0m);
        result.Value.Collections.PaidAmount.Should().Be(0m);
        result.Value.Collections.PendingAmount.Should().Be(0m);
        result.Value.Collections.AvgTicket.Should().Be(0m);
        result.Value.Days.Should().OnlyContain(d => d.RetailAmount == 0m && d.CurrentAccountAmount == 0m);
        result.Value.RecentSales.Should().OnlyContain(s => s.TotalAmount == 0m);
    }
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj --filter "FullyQualifiedName~SinPermisoFinanciero"`
Expected: FAIL — los importes vienen con los valores reales.

- [ ] **Step 3: Aplicar el gating**

En `Handle`, después de calcular `canViewAllBranches`, agregar:

```csharp
        var canViewFinancials = _currentUserService.HasPermission(PermissionCodes.DashboardViewFinancials);
```

Y antes del `return`, envolver la respuesta:

```csharp
        var response = new GetDashboardSummaryResponse(
            month, today, days, topProducts, collections, todayStatus, recentSales);

        return Result<GetDashboardSummaryResponse>.Success(
            canViewFinancials ? response : StripAmounts(response));
```

Agregar el método:

```csharp
    // La plata no se esconde en el front: no sale del server. Antes viajaba en listSales y
    // quedaba visible en el network tab de cualquiera con solo mirar.
    private static GetDashboardSummaryResponse StripAmounts(GetDashboardSummaryResponse source) =>
        source with
        {
            Month = StripTotals(source.Month),
            Today = StripTotals(source.Today),
            Days = source.Days
                .Select(d => d with { RetailAmount = 0m, CurrentAccountAmount = 0m })
                .ToList(),
            Collections = new DashboardCollections(
                0m, source.Collections.PaidCount, 0m, source.Collections.PendingCount, 0m),
            RecentSales = source.RecentSales.Select(s => s with { TotalAmount = 0m }).ToList()
        };

    private static DashboardPeriodTotals StripTotals(DashboardPeriodTotals totals) =>
        new(totals.Total with { Amount = 0m },
            totals.Retail with { Amount = 0m },
            totals.CurrentAccount with { Amount = 0m });
```

- [ ] **Step 4: Correr toda la suite**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: `Correctas!` — 0 con error, 9 tests del handler pasando.

- [ ] **Step 5: Commit**

```bash
cd C:/Eiti/eiti
git add eiti.Application/Features/Dashboard eiti.Tests/GetDashboardSummaryHandlerTests.cs
git commit -m "feat(dashboard): los importes no salen del server sin permiso financiero

Sin dashboard.view_financials todos los montos se devuelven en 0, manteniendo las
cantidades. Antes listSales mandaba todos los importes y el front los escondia con
un ngIf, o sea que estaban en el network tab de cualquiera.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Controller y verificación contra la base real

**Files:**
- Create: `eiti.Api/Controllers/DashboardController.cs`

**Interfaces:**
- Consumes: `GetDashboardSummaryQuery`, `ISender`, `ResultExtensions.ToActionResult()`.
- Produces: `GET /api/dashboard/summary?dateFrom=&dateTo=&branchId=`.

- [ ] **Step 1: Crear el controller**

`eiti.Api/Controllers/DashboardController.cs`:

```csharp
using eiti.Api.Extensions;
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eiti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetDashboardSummaryQuery(dateFrom, dateTo, branchId),
            cancellationToken);
        return result.ToActionResult();
    }
}
```

- [ ] **Step 2: Compilar**

Run: `cd C:/Eiti/eiti && dotnet build eiti.Api/eiti.Api.csproj`
Expected: `0 Errores`

- [ ] **Step 3: Verificar contra la base real que la consulta traduce y devuelve datos**

Compilar no prueba que EF traduzca la consulta ni que el handler devuelva números correctos. Crear un test temporal `eiti.Tests/TempDashboardCheck.cs`:

```csharp
using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using eiti.Infrastructure.Persistence;
using eiti.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace eiti.Tests;

// TEMPORAL: corre el repositorio contra la base real. Solo SELECT. Se borra despues.
public sealed class TempDashboardCheck
{
    private readonly ITestOutputHelper _output;
    public TempDashboardCheck(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ListForSalesReport_DevuelveVentasDelMes()
    {
        var conn = Environment.GetEnvironmentVariable("EITI_TEST_CONN");
        Assert.False(string.IsNullOrWhiteSpace(conn), "falta EITI_TEST_CONN");

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await using var ctx = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conn).Options);
        var repo = new SaleRepository(ctx);

        var companyId = new eiti.Domain.Companies.CompanyId(
            Guid.Parse("e467f46f-9434-4c19-bab0-51e1b5bbc64e"));
        var (from, to) = eiti.Application.Common.BusinessCalendar.ToUtcRange(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        var sales = await repo.ListForSalesReportAsync(companyId, from, to, null, null, null);

        _output.WriteLine($"ventas={sales.Count}");
        _output.WriteLine($"minorista={sales.Count(s => !s.IsCuentaCorriente)}");
        _output.WriteLine($"cc={sales.Count(s => s.IsCuentaCorriente)}");
        _output.WriteLine($"con detalles={sales.Count(s => s.Details.Count > 0)}");
        Assert.NotEmpty(sales);
        Assert.All(sales, s => Assert.NotNull(s.Details));
    }
}
```

Correr:

```bash
cd C:/Eiti/eiti
railway variables --service Postgres --kv | grep '^DATABASE_PUBLIC_URL=' | cut -d= -f2- > /tmp/url.txt
python -c "
import urllib.parse as up
u = up.urlparse(open('/tmp/url.txt').read().strip())
print(f\"Host={u.hostname};Port={u.port};Database={u.path.lstrip('/')};Username={u.username};Password={up.unquote(u.password)};SSL Mode=Require;Trust Server Certificate=true\", end='')
" > /tmp/conn.txt
EITI_TEST_CONN="$(cat /tmp/conn.txt)" dotnet test eiti.Tests/eiti.Tests.csproj \
  --filter "FullyQualifiedName~TempDashboardCheck" --logger "console;verbosity=detailed"
```

Expected: PASS, con `ventas=` mayor a 0 y `con detalles=` mayor a 0.

- [ ] **Step 4: Borrar el test temporal y correr la suite**

```bash
cd C:/Eiti/eiti && rm eiti.Tests/TempDashboardCheck.cs
dotnet test eiti.Tests/eiti.Tests.csproj
```
Expected: `Correctas!`, 0 con error.

- [ ] **Step 5: Commit**

```bash
cd C:/Eiti/eiti
git add eiti.Api/Controllers/DashboardController.cs
git commit -m "feat(dashboard): endpoint GET /api/dashboard/summary

Verificado contra la base real que la consulta traduce y devuelve las ventas del
mes con sus detalles.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Front — modelos y servicio HTTP

**Files:**
- Create: `C:/EiTeFront/eiti-front/src/app/core/models/dashboard.models.ts`
- Create: `C:/EiTeFront/eiti-front/src/app/core/services/dashboard.service.ts`

**Interfaces:**
- Consumes: `environment.apiUrl`, `HttpClient`.
- Produces: `DashboardService.getSummary(dateFrom: string, dateTo: string, branchId?: string | null): Observable<DashboardSummaryResponse>` y las interfaces del modelo.

- [ ] **Step 1: Crear los modelos**

`src/app/core/models/dashboard.models.ts`:

```ts
export interface DashboardSegment {
    count: number;
    amount: number;
}

export interface DashboardPeriodTotals {
    total: DashboardSegment;
    retail: DashboardSegment;
    currentAccount: DashboardSegment;
}

export interface DashboardDayPoint {
    date: string;
    retailCount: number;
    retailAmount: number;
    currentAccountCount: number;
    currentAccountAmount: number;
}

export interface DashboardTopProduct {
    productId: string;
    name: string;
    brand: string;
    units: number;
    salesCount: number;
}

export interface DashboardCollections {
    paidAmount: number;
    paidCount: number;
    pendingAmount: number;
    pendingCount: number;
    avgTicket: number;
}

export interface DashboardTodayStatus {
    activeCount: number;
    paidCount: number;
    pendingCount: number;
    cancelledCount: number;
}

export interface DashboardRecentSale {
    id: string;
    code?: string | null;
    createdAt: string;
    customerName: string;
    saleStatus: number;
    totalAmount: number;
    isCuentaCorriente: boolean;
}

export interface DashboardSummaryResponse {
    month: DashboardPeriodTotals;
    today: DashboardPeriodTotals;
    days: DashboardDayPoint[];
    topProducts: DashboardTopProduct[];
    collections: DashboardCollections;
    todayStatus: DashboardTodayStatus;
    recentSales: DashboardRecentSale[];
}

/** Serie visible en el gráfico de ritmo comercial. */
export type DashboardChartSegment = 'both' | 'retail' | 'cc';

/** Métrica del panel comercial. 'products' cambia la vista al ranking. */
export type DashboardChartMetric = 'count' | 'amount' | 'products';
```

- [ ] **Step 2: Crear el servicio**

`src/app/core/services/dashboard.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardSummaryResponse } from '../models/dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
    private readonly base = `${environment.apiUrl}/dashboard`;

    constructor(private readonly http: HttpClient) {}

    getSummary(dateFrom: string, dateTo: string, branchId?: string | null): Observable<DashboardSummaryResponse> {
        const params = new URLSearchParams();
        params.set('dateFrom', dateFrom);
        params.set('dateTo', dateTo);
        if (branchId) {
            params.set('branchId', branchId);
        }
        return this.http.get<DashboardSummaryResponse>(`${this.base}/summary?${params.toString()}`);
    }
}
```

- [ ] **Step 3: Compilar el front**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: `Build at:` sin líneas `error TS`.

- [ ] **Step 4: Commit**

```bash
cd C:/EiTeFront/eiti-front && git checkout develop
git add src/app/core/models/dashboard.models.ts src/app/core/services/dashboard.service.ts
git commit -m "feat(dashboard): modelos y servicio del resumen

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Front — servicio de preferencias con validación al restaurar

**Files:**
- Create: `C:/EiTeFront/eiti-front/src/app/core/services/dashboard-preferences.service.ts`
- Test: `C:/EiTeFront/eiti-front/src/app/core/services/dashboard-preferences.service.spec.ts`

**Interfaces:**
- Produces: `DashboardPreferencesService` con `readBranchId(availableBranchIds: string[], canViewAll: boolean): string | null`, `writeBranchId(value: string | null): void`, `readChartSegment(): DashboardChartSegment`, `writeChartSegment(value: DashboardChartSegment): void`, `readChartMetric(): DashboardChartMetric`, `writeChartMetric(value: DashboardChartMetric): void`.

- [ ] **Step 1: Escribir los tests que fallan**

`src/app/core/services/dashboard-preferences.service.spec.ts`:

```ts
import { DashboardPreferencesService } from './dashboard-preferences.service';

describe('DashboardPreferencesService', () => {
  let service: DashboardPreferencesService;

  beforeEach(() => {
    localStorage.clear();
    service = new DashboardPreferencesService();
  });

  it('devuelve null cuando no hay nada guardado y puede ver todas', () => {
    expect(service.readBranchId(['a', 'b'], true)).toBeNull();
  });

  it('restaura la sucursal guardada si sigue disponible', () => {
    service.writeBranchId('a');
    expect(service.readBranchId(['a', 'b'], true)).toBe('a');
  });

  it('descarta la sucursal guardada si ya no esta disponible', () => {
    service.writeBranchId('z');
    expect(service.readBranchId(['a', 'b'], true)).toBeNull();
  });

  it('sin permiso de ver todas, null cae a la primera sucursal', () => {
    expect(service.readBranchId(['a', 'b'], false)).toBe('a');
  });

  it('sin permiso de ver todas, una sucursal guardada invalida cae a la primera', () => {
    service.writeBranchId('z');
    expect(service.readBranchId(['a', 'b'], false)).toBe('a');
  });

  it('guarda y restaura la serie del grafico', () => {
    service.writeChartSegment('cc');
    expect(service.readChartSegment()).toBe('cc');
  });

  it('descarta un valor de serie invalido', () => {
    localStorage.setItem('eiti_dashboard_chart_segment', 'basura');
    expect(service.readChartSegment()).toBe('both');
  });

  it('descarta una metrica invalida', () => {
    localStorage.setItem('eiti_dashboard_chart_metric', 'basura');
    expect(service.readChartMetric()).toBe('count');
  });
});
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Run: `cd C:/EiTeFront/eiti-front && ng test --watch=false --browsers=ChromeHeadless --include='**/dashboard-preferences.service.spec.ts'`
Expected: FAIL — el módulo no existe.

Si el proyecto no tiene runner de tests configurado, verificarlo con `cat angular.json | grep -A 5 '"test"'`. Si no hay target de test, saltear los pasos 2 y 4 y validar la lógica con el build más una prueba manual en el navegador; dejarlo anotado en el commit.

- [ ] **Step 3: Escribir el servicio**

`src/app/core/services/dashboard-preferences.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { DashboardChartMetric, DashboardChartSegment } from '../models/dashboard.models';

const BRANCH_KEY = 'eiti_dashboard_branch_id';
const SEGMENT_KEY = 'eiti_dashboard_chart_segment';
const METRIC_KEY = 'eiti_dashboard_chart_metric';

const SEGMENTS: readonly DashboardChartSegment[] = ['both', 'retail', 'cc'];
const METRICS: readonly DashboardChartMetric[] = ['count', 'amount', 'products'];

/**
 * Preferencias del dashboard. Persiste solo la configuración estable (sucursal, serie y
 * métrica del gráfico); el drill-down por día o estado es exploración del momento y no se
 * guarda. Toda lectura valida contra lo que hoy es válido: una sucursal guardada que el
 * usuario ya no puede ver se descarta en vez de romper la pantalla.
 */
@Injectable({ providedIn: 'root' })
export class DashboardPreferencesService {
    readBranchId(availableBranchIds: string[], canViewAll: boolean): string | null {
        const stored = this.read(BRANCH_KEY);
        const isValid = stored !== null && availableBranchIds.includes(stored);

        if (isValid) {
            return stored;
        }

        // Sin permiso de ver todas, "Todas" no es una opción: cae a la primera asignada.
        return canViewAll ? null : (availableBranchIds[0] ?? null);
    }

    writeBranchId(value: string | null): void {
        if (value === null) {
            this.remove(BRANCH_KEY);
            return;
        }
        this.write(BRANCH_KEY, value);
    }

    readChartSegment(): DashboardChartSegment {
        const stored = this.read(SEGMENT_KEY);
        return SEGMENTS.includes(stored as DashboardChartSegment)
            ? (stored as DashboardChartSegment)
            : 'both';
    }

    writeChartSegment(value: DashboardChartSegment): void {
        this.write(SEGMENT_KEY, value);
    }

    readChartMetric(): DashboardChartMetric {
        const stored = this.read(METRIC_KEY);
        return METRICS.includes(stored as DashboardChartMetric)
            ? (stored as DashboardChartMetric)
            : 'count';
    }

    writeChartMetric(value: DashboardChartMetric): void {
        this.write(METRIC_KEY, value);
    }

    private read(key: string): string | null {
        try {
            return localStorage.getItem(key);
        } catch {
            // Una preferencia visual nunca debe romper la pantalla.
            return null;
        }
    }

    private write(key: string, value: string): void {
        try {
            localStorage.setItem(key, value);
        } catch {
            // idem
        }
    }

    private remove(key: string): void {
        try {
            localStorage.removeItem(key);
        } catch {
            // idem
        }
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `cd C:/EiTeFront/eiti-front && ng test --watch=false --browsers=ChromeHeadless --include='**/dashboard-preferences.service.spec.ts'`
Expected: 8 specs pasando.

- [ ] **Step 5: Compilar**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: sin `error TS`.

- [ ] **Step 6: Commit**

```bash
cd C:/EiTeFront/eiti-front
git add src/app/core/services/dashboard-preferences.service.ts src/app/core/services/dashboard-preferences.service.spec.ts
git commit -m "feat(dashboard): persistencia de filtros con validacion al restaurar

Guarda sucursal, serie y metrica del grafico. El drill-down por dia o estado no se
persiste. Una sucursal guardada que el usuario ya no puede ver se descarta.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: Front — reescritura del componente

**IMPORTANTE:** antes de escribir una línea de este componente hay que **invocar la skill `frontend-design`**, según el `CLAUDE.md` del proyecto. Esta tarea no arranca sin eso.

**Files:**
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.ts`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.html`
- Modify: `C:/EiTeFront/eiti-front/src/app/features/dashboard/dashboard.component.css`

**Interfaces:**
- Consumes: `DashboardService.getSummary(...)`, `DashboardPreferencesService`, `BranchService.listBranches()`, `AuthService` (`hasPermission`, `currentUser.canViewAllBranches`), `ToastService`, `SaleService.listSales({dateFrom, dateTo, includeCuentaCorriente})` para el drill-down.
- Produces: nada que otro código consuma.

- [ ] **Step 1: Invocar la skill de diseño**

Invocar `frontend-design` con el contexto: rediseño de jerarquía del dashboard, paleta existente, dos series `--amber` / `--success`, verificación en `theme-dark` y `theme-light`.

- [ ] **Step 2: Reescribir el componente TypeScript**

Estado del componente (los ~35 getters derivados desaparecen: el server ya devuelve todo calculado):

```ts
summary: DashboardSummaryResponse | null = null;
branches: BranchResponse[] = [];
loading = true;
loadFailed = false;
branchId: string | null = null;
chartSegment: DashboardChartSegment = 'both';
chartMetric: DashboardChartMetric = 'count';
selectedDayKey: string | null = null;
selectedStatusKey: 'paid' | 'pending' | 'cancelled' | null = null;
drillDownSales: SaleResponse[] = [];
```

Flujo de `ngOnInit`:

1. `chartSegment = prefs.readChartSegment()`, `chartMetric = prefs.readChartMetric()`.
2. `branchService.listBranches()` → al llegar, `branchId = prefs.readBranchId(branches.map(b => b.id), auth.currentUser?.canViewAllBranches ?? false)`.
3. `loadSummary()`.
4. Si `listBranches` falla: `branches = []`, `branchId = null`, se oculta el selector, se sigue igual.

`loadSummary()`:

```ts
private loadSummary(): void {
  this.loading = true;
  this.loadFailed = false;
  const now = new Date();
  const dateFrom = new Date(now.getFullYear(), now.getMonth(), 1).toLocaleDateString('en-CA');
  const dateTo = new Date(now.getFullYear(), now.getMonth() + 1, 0).toLocaleDateString('en-CA');

  this.dashboardService.getSummary(dateFrom, dateTo, this.branchId).subscribe({
    next: summary => { this.summary = summary; this.loading = false; },
    error: (err: { error?: { detail?: string } }) => {
      this.loading = false;
      this.loadFailed = true;
      this.toast.error(err?.error?.detail || 'No se pudo cargar el dashboard.');
    }
  });
}
```

**Usar `toLocaleDateString('en-CA')`, nunca `toISOString().slice(0,10)`** — este último devuelve la fecha UTC y después de las 21:00 hora Argentina da el día siguiente.

Handlers:

- `setBranch(id: string | null)`: asigna, `prefs.writeBranchId(id)`, limpia `selectedDayKey`/`selectedStatusKey`, `loadSummary()`.
- `setChartSegment(s)`: asigna y `prefs.writeChartSegment(s)`. **Sin request.**
- `setChartMetric(m)`: si `m === 'amount'` y no hay permiso, return. Asigna y `prefs.writeChartMetric(m)`. **Sin request.**
- `selectDay(dateKey)`: toggle; si queda seleccionado, `saleService.listSales({ dateFrom: dateKey, dateTo: dateKey, includeCuentaCorriente: true })` y filtrar en cliente por `branchId` si no es null.
- `selectStatus(key)`: toggle, filtra en cliente sobre `drillDownSales` o `summary.recentSales`.

- [ ] **Step 3: Reescribir el HTML con la jerarquía nueva**

Orden exacto de secciones:

1. `header` con título y el selector de sucursal. El selector solo se renderiza si `branches.length > 1`. Incluye la opción "Todas" solo si `auth.currentUser?.canViewAllBranches`.
2. **Lectura del mes en curso** (primer plano): tabla con filas TOTAL / Minorista / Cta. Corriente y columnas MES y HOY. La columna de importe solo se renderiza si `canViewFinancials`. **No mostrar `$0`** cuando falta el permiso: no se renderiza la celda.
3. **Ritmo comercial** (primer plano): toggle `Ambas | Minorista | CC`, toggle `Cantidad | Monto | Productos` (Monto solo con permiso), y las barras. En modo `products`, el ranking `summary.topProducts`.
4. **Ventas**: `summary.recentSales`, o el drill-down si hay día/estado seleccionado.
5. Sección secundaria con tres paneles: **Cobranza** (`collections`), **Pulso del día** (`todayStatus`), **Alertas operativas**.

Estado de carga: mientras `loading`, los esqueletos que ya existen. Si `loadFailed`, un estado vacío con botón que llama a `loadSummary()`.

- [ ] **Step 4: Ajustar el CSS**

Reutilizar las clases existentes (`hero-card`, `kpi-card`, `data-panel`, `panel-toggle`, `pulse-card`) reordenándolas. Para las dos series del gráfico agregar únicamente:

```css
/* Series del ritmo comercial. Ambas salen de tokens ya definidos en los dos temas. */
.chart-bar--retail { background: color-mix(in srgb, var(--amber) 85%, transparent); }
.chart-bar--cc     { background: color-mix(in srgb, var(--success) 80%, transparent); }
```

Cero colores hardcodeados.

- [ ] **Step 5: Compilar**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: sin `error TS`.

- [ ] **Step 6: Verificar la tabla de destino de widgets**

Comparar contra la tabla del spec y confirmar uno por uno que ninguno se perdió: Resumen de hoy (absorbido), Lectura del mes (promovido), Alertas (bajó), Ingresos cobrados + Pendiente (bajaron a Cobranza), Ticket promedio (bajó a Cobranza), Ritmo comercial (arriba, dos series), Pulso del día (bajó), Últimas ventas + drill-down (se queda).

- [ ] **Step 7: Verificar los dos temas**

Abrir el dashboard y alternar `theme-dark` / `theme-light`. Confirmar que las dos series se distinguen y que ningún texto queda ilegible.

- [ ] **Step 8: Commit**

```bash
cd C:/EiTeFront/eiti-front
git add src/app/features/dashboard
git commit -m "feat(dashboard): nueva jerarquia con lectura del mes en primer plano

La pantalla pasa a consumir /api/dashboard/summary en vez de derivar ~35 getters
del mes entero de ventas. Lectura del mes en curso al frente con columnas MES y HOY
cortadas por minorista y cuenta corriente, grafico con dos series, filtro global de
sucursal con persistencia, y alertas, cobranza y pulso del dia en seccion secundaria.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 10: Verificación end-to-end y deploy

**Files:** ninguno.

- [ ] **Step 1: Suite completa del backend**

Run: `cd C:/Eiti/eiti && dotnet test eiti.Tests/eiti.Tests.csproj`
Expected: `Correctas!`, 0 con error.

- [ ] **Step 2: Build del front**

Run: `cd C:/EiTeFront/eiti-front && ng build --configuration development`
Expected: sin `error TS`.

- [ ] **Step 3: Merge a main y tag en ambos repos**

```bash
cd C:/Eiti/eiti && git push origin develop && git checkout main && git merge --ff-only develop
git tag -a v1.13.0 -m "v1.13.0 - Dashboard con endpoint agregado + compresion de respuestas"
git push origin main && git push origin v1.13.0

cd C:/EiTeFront/eiti-front && git push origin develop && git checkout main && git merge --ff-only develop
git tag -a v1.12.0 -m "v1.12.0 - Rediseno del dashboard"
git push origin main && git push origin v1.12.0
```

- [ ] **Step 4: Esperar el deploy de Railway**

```bash
cd C:/Eiti/eiti
for i in $(seq 1 12); do
  L=$(railway deployment list --service eiti-api 2>/dev/null | grep -E "\|" | head -1)
  echo "[$i] $L"
  case "$L" in *SUCCESS*) echo ">> LISTO"; break;; esac
  sleep 25
done
```

- [ ] **Step 5: Deploy del front**

```bash
cd C:/EiTeFront/eiti-front && vercel deploy --prod --scope agustin-testa-s-projects1
```

- [ ] **Step 6: Verificar que la compresión quedó activa**

```bash
curl -s -I -H "Accept-Encoding: br, gzip" "https://api.eiticloud.com/api/dashboard/summary?dateFrom=2026-08-01&dateTo=2026-08-31" | grep -iE "content-encoding|HTTP"
```

Expected: `401` (sin token, correcto) y el endpoint existiendo. Para ver `content-encoding` hace falta una respuesta con cuerpo; verificar con una request autenticada desde el navegador mirando el tamaño en el network tab.

- [ ] **Step 7: Verificar el bundle del front en producción**

El dashboard es un chunk lazy, así que `main.js` puede no cambiar de hash. Verificar que el deploy servido es el nuevo:

```bash
cd C:/EiTeFront/eiti-front && vercel inspect app.eiticloud.com --scope agustin-testa-s-projects1 | grep -iE "url|created|status"
```

Expected: `created` de hace pocos minutos y el mismo id que devolvió el deploy.

- [ ] **Step 8: Verificación funcional en producción**

Entrar a `app.eiticloud.com/dashboard` y confirmar:
- El bloque del mes muestra TOTAL / Minorista / Cta. Corriente en columnas MES y HOY.
- Cambiar de sucursal actualiza los números.
- Cambiar `Ambas | Minorista | CC` es instantáneo, sin request en el network tab.
- Recargar la página mantiene la sucursal y la serie elegidas.
- Con un usuario sin `dashboard.view_financials`, no aparece ninguna columna de importes ni `$0`.
