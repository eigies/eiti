using System.Text.Json;
using eiti.Api.Controllers;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Companies.Commands.SetRetailDiscountLimit;
using eiti.Application.Features.Companies.Queries.GetCurrentCompany;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using MediatR;
using Moq;

namespace eiti.Tests;

// Descuento minorista con tope: el dueño fija el máximo y cada descuento general de la venta queda trazado
// (quién, cuándo, por qué).
public sealed class RetailDiscountLimitTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static Sale BuildSale(decimal generalDiscountPercent = 0m)
    {
        var companyId = CompanyId.New();
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Moura", "Bateria", null, 100000m, 70000m, null);
        return Sale.Create(
            companyId, BranchId.New(), null, false, SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)],
            generalDiscountPercent: generalDiscountPercent);
    }

    // --- Trazabilidad en la venta ---

    [Fact]
    public void TrackGeneralDiscount_RecordsWhoWhenAndWhy_WhenTheDiscountIsNew()
    {
        var sale = BuildSale(generalDiscountPercent: 10m);
        var userId = Guid.NewGuid();

        sale.TrackGeneralDiscount(previousPercent: 0m, userId, "  Cliente de años  ");

        sale.GeneralDiscountByUserId.Should().Be(userId);
        sale.GeneralDiscountAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        sale.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public void TrackGeneralDiscount_KeepsTheOriginalTrace_WhenThePercentDidNotChange()
    {
        var sale = BuildSale(generalDiscountPercent: 10m);
        var seller = Guid.NewGuid();
        sale.TrackGeneralDiscount(0m, seller, "Cliente de años");
        var firstAt = sale.GeneralDiscountAt;

        sale.TrackGeneralDiscount(previousPercent: 10m, Guid.NewGuid(), "Otro motivo");

        sale.GeneralDiscountByUserId.Should().Be(seller);
        sale.GeneralDiscountAt.Should().Be(firstAt);
        sale.GeneralDiscountReason.Should().Be("Cliente de años");
    }

    [Fact]
    public void TrackGeneralDiscount_ClearsTheTrace_WhenTheDiscountGoesBackToZero()
    {
        var sale = BuildSale(generalDiscountPercent: 10m);
        sale.TrackGeneralDiscount(0m, Guid.NewGuid(), "Cliente de años");
        var detail = SaleDetail.Create(sale.Details.First().ProductId, 1, 100000m);
        sale.Update(null, SaleStatus.OnHold, false, [detail], generalDiscountPercent: 0m);

        sale.TrackGeneralDiscount(previousPercent: 10m, Guid.NewGuid(), "Motivo");

        sale.GeneralDiscountByUserId.Should().BeNull();
        sale.GeneralDiscountAt.Should().BeNull();
        sale.GeneralDiscountReason.Should().BeNull();
    }

    [Fact]
    public void TrackGeneralDiscount_BlankReasonMeansNoReason()
    {
        var sale = BuildSale(generalDiscountPercent: 5m);

        sale.TrackGeneralDiscount(0m, Guid.NewGuid(), "   ");

        sale.GeneralDiscountReason.Should().BeNull();
    }

    [Fact]
    public void TrackGeneralDiscount_RejectsAReasonOverTheMaximum()
    {
        var sale = BuildSale(generalDiscountPercent: 5m);

        var act = () => sale.TrackGeneralDiscount(0m, Guid.NewGuid(), new string('x', Sale.GeneralDiscountReasonMaxLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    // --- Tope de la empresa ---

    [Fact]
    public void Company_StartsWithTheDiscountDisabled()
    {
        var company = Company.Create(CompanyName.Create("Baterías Soler"), CompanyDomain.Create("soler.com"));

        company.MaxRetailDiscountPercent.Should().Be(0m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Company_RejectsALimitOutOfRange(double value)
    {
        var company = Company.Create(CompanyName.Create("Baterías Soler"), CompanyDomain.Create("soler.com"));

        var act = () => company.SetMaxRetailDiscountPercent((decimal)value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task SetRetailDiscountLimit_SavesTheLimitOnTheCurrentCompany()
    {
        var company = Company.Create(CompanyName.Create("Baterías Soler"), CompanyDomain.Create("soler.com"));
        var (handler, unitOfWork) = BuildHandler(company);

        var result = await handler.Handle(new SetRetailDiscountLimitCommand(12.5m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        company.MaxRetailDiscountPercent.Should().Be(12.5m);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetRetailDiscountLimit_FailsWhenTheCompanyDoesNotExist()
    {
        var (handler, unitOfWork) = BuildHandler(null);

        var result = await handler.Handle(new SetRetailDiscountLimitCommand(10m), CancellationToken.None);

        result.Error.Should().Be(SetRetailDiscountLimitErrors.CompanyNotFound);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetRetailDiscountLimitValidator_RejectsOutOfRange(double value)
    {
        var result = new SetRetailDiscountLimitValidator().Validate(new SetRetailDiscountLimitCommand((decimal)value));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(SetRetailDiscountLimitCommand.MaxPercent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SetRetailDiscountLimitValidator_AcceptsTheBounds(double value)
    {
        var result = new SetRetailDiscountLimitValidator().Validate(new SetRetailDiscountLimitCommand((decimal)value));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SetRetailDiscountLimit_RequiresTheConfigurePermission()
    {
        new SetRetailDiscountLimitCommand(10m).RequiredPermissions
            .Should().Equal(PermissionCodes.SalesDiscountConfigure);
    }

    [Fact]
    public async Task GetCurrentCompany_ReturnsTheLimit()
    {
        var company = Company.Create(CompanyName.Create("Baterías Soler"), CompanyDomain.Create("soler.com"));
        company.SetMaxRetailDiscountPercent(10m);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(company.Id);
        var companies = new Mock<ICompanyRepository>();
        companies.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var result = await new GetCurrentCompanyHandler(currentUser.Object, companies.Object)
            .Handle(new GetCurrentCompanyQuery(), CancellationToken.None);

        result.Value.MaxRetailDiscountPercent.Should().Be(10m);
    }

    // El tope tiene que llegar del JSON del PUT al comando (lección 2026-10-23).
    [Fact]
    public async Task Endpoint_TheLimitTravelsFromTheJsonToTheCommand()
    {
        var command = JsonSerializer.Deserialize<SetRetailDiscountLimitCommand>("""{ "maxPercent": 12.5 }""", Web)!;
        SetRetailDiscountLimitCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<SetRetailDiscountLimitCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result>, CancellationToken>((c, _) => sent = (SetRetailDiscountLimitCommand)c)
            .ReturnsAsync(Result.Success());

        await new CompaniesController(sender.Object).SetRetailDiscountLimit(command, CancellationToken.None);

        sent!.MaxPercent.Should().Be(12.5m);
    }

    // --- Permisos: en las tres listas del back ---

    [Fact]
    public void Permissions_AreValidForAccessProfiles()
    {
        PermissionCatalog.IsValid(PermissionCodes.SalesDiscountConfigure).Should().BeTrue();
        PermissionCatalog.IsValid(PermissionCodes.ReportsSalesDiscounts).Should().BeTrue();
        PermissionCodes.SalesDiscountConfigure.Should().Be("sales.discount.configure");
        PermissionCodes.ReportsSalesDiscounts.Should().Be("reports.sales.discounts");
    }

    [Fact]
    public void Permissions_TheLimitIsOnlyForTheOwner()
    {
        RoleCatalog.All.Where(r => r.Permissions.Contains(PermissionCodes.SalesDiscountConfigure))
            .Select(r => r.Code)
            .Should().Equal(SystemRoles.Owner);
    }

    [Fact]
    public void Permissions_TheReportGoesToTheSameRolesAsTheTradeInsReport()
    {
        var withTradeIns = RoleCatalog.All.Where(r => r.Permissions.Contains(PermissionCodes.ReportsSalesTradeIns)).Select(r => r.Code);
        var withDiscounts = RoleCatalog.All.Where(r => r.Permissions.Contains(PermissionCodes.ReportsSalesDiscounts)).Select(r => r.Code);

        withDiscounts.Should().BeEquivalentTo(withTradeIns).And.Contain(SystemRoles.Owner);
    }

    private static (SetRetailDiscountLimitHandler Handler, Mock<IUnitOfWork> UnitOfWork) BuildHandler(Company? company)
    {
        var companyId = company?.Id ?? CompanyId.New();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
        currentUser.SetupGet(s => s.CompanyId).Returns(companyId);
        var companies = new Mock<ICompanyRepository>();
        companies.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        var unitOfWork = new Mock<IUnitOfWork>();
        return (new SetRetailDiscountLimitHandler(currentUser.Object, companies.Object, unitOfWork.Object), unitOfWork);
    }
}
