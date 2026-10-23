using System.Net;
using System.Text;
using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.FiscalSettings.Commands.AssignFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.CreateFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.DeleteFiscalPointOfSale;
using eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace eiti.Tests;

/// <summary>
/// Facturación electrónica configurable por el cliente: puntos de venta atados 1:1 a sucursales y
/// datos del emisor que viven en el servicio de facturación.
/// </summary>
public sealed class FiscalSettingsTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly Branch _centro;
    private readonly Branch _norte;
    private readonly List<FiscalPointOfSale> _stored = [];
    private readonly Mock<IFiscalPointOfSaleRepository> _pointsOfSale = new();
    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<IFiscalizationService> _fiscal = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public FiscalSettingsTests()
    {
        _centro = Branch.Create(_companyId, "Centro", "C", "San Martin 123");
        _norte = Branch.Create(_companyId, "Norte", "N", "Cabildo 2000");
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        _fiscal.SetupGet(x => x.IsEnabled).Returns(true);
        _fiscal.Setup(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalOperationResult(true));
        _pointsOfSale.Setup(x => x.AddAsync(It.IsAny<FiscalPointOfSale>(), It.IsAny<CancellationToken>()))
            .Callback<FiscalPointOfSale, CancellationToken>((p, _) => _stored.Add(p))
            .Returns(Task.CompletedTask);
        _pointsOfSale.Setup(x => x.GetByIdAsync(It.IsAny<FiscalPointOfSaleId>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FiscalPointOfSaleId id, CompanyId _, CancellationToken _) => _stored.FirstOrDefault(p => p.Id == id));
        foreach (var branch in new[] { _centro, _norte })
        {
            _branches.Setup(x => x.GetByIdAsync(branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        }
        _branches.Setup(x => x.GetByFiscalPointOfSaleIdAsync(It.IsAny<FiscalPointOfSaleId>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FiscalPointOfSaleId id, CompanyId _, CancellationToken _) =>
                new[] { _centro, _norte }.FirstOrDefault(b => b.FiscalPointOfSaleId == id));
    }

    [Fact]
    public async Task Loading_a_point_of_sale_with_a_branch_enables_it_and_ties_it()
    {
        var result = await Create(3, _centro.Id.Value);

        result.Value.Should().BeEquivalentTo(new { Number = 3, BranchId = (Guid?)_centro.Id.Value, BranchName = "Centro" });
        _centro.FiscalPointOfSale!.Number.Should().Be(3);
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(_companyId.Value, 3, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_point_of_sale_can_be_loaded_without_a_branch()
    {
        var result = await Create(3, null);

        result.Value.BranchId.Should().BeNull();
        _stored.Should().ContainSingle(p => p.Number == 3);
    }

    [Fact]
    public async Task If_the_fiscal_service_refuses_it_nothing_is_saved()
    {
        _fiscal.Setup(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalOperationResult(false, "El servicio de facturación todavía no tiene un perfil fiscal activo para esta empresa."));

        var result = await Create(3, null);

        result.Error.Code.Should().Be("FiscalPointsOfSale.NotRegistered");
        _stored.Should().BeEmpty();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_branch_that_already_invoices_is_not_overwritten_and_the_service_is_not_called()
    {
        await Create(3, _centro.Id.Value);
        _fiscal.Invocations.Clear();

        var result = await Create(4, _centro.Id.Value);

        result.Error.Code.Should().Be("FiscalPointsOfSale.BranchHasAnother");
        result.Error.Description.Should().Be("La sucursal Centro ya factura con el punto de venta 3. Sacáselo primero.");
        _fiscal.Verify(x => x.RegisterPointOfSaleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_number_already_loaded_is_refused()
    {
        _pointsOfSale.Setup(x => x.NumberInUseAsync(_companyId, 3, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Create(3, null);

        result.Error.Code.Should().Be("FiscalPointsOfSale.InUse");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100000)]
    public async Task An_out_of_range_number_is_refused(int number)
    {
        (await Create(number, null)).Error.Code.Should().Be("FiscalPointsOfSale.OutOfRange");
    }

    [Fact]
    public async Task Assigning_to_another_branch_moves_it()
    {
        var created = await Create(3, _centro.Id.Value);

        var result = await Assign(created.Value.Id, _norte.Id.Value);

        result.Value.BranchName.Should().Be("Norte");
        _norte.FiscalPointOfSale!.Number.Should().Be(3);
        _centro.FiscalPointOfSaleId.Should().BeNull("a point of sale belongs to a single branch");
    }

    [Fact]
    public async Task Assigning_null_leaves_it_free()
    {
        var created = await Create(3, _centro.Id.Value);

        var result = await Assign(created.Value.Id, null);

        result.Value.BranchId.Should().BeNull();
        _centro.FiscalPointOfSaleId.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_point_of_sale_is_not_found()
    {
        (await Assign(Guid.NewGuid(), _centro.Id.Value)).Error.Code.Should().Be("FiscalPointsOfSale.NotFound");
    }

    [Fact]
    public async Task A_point_of_sale_in_use_cannot_be_deleted()
    {
        var created = await Create(3, _centro.Id.Value);

        var result = await Delete(created.Value.Id);

        result.Error.Description.Should().Be("El punto de venta lo usa la sucursal Centro. Desasignalo antes de borrarlo.");
        _pointsOfSale.Verify(x => x.Remove(It.IsAny<FiscalPointOfSale>()), Times.Never);
    }

    [Fact]
    public async Task A_free_point_of_sale_is_deleted()
    {
        var created = await Create(3, null);

        (await Delete(created.Value.Id)).IsSuccess.Should().BeTrue();
        _pointsOfSale.Verify(x => x.Remove(It.Is<FiscalPointOfSale>(p => p.Number == 3)), Times.Once);
    }

    [Fact]
    public async Task Automatic_invoicing_is_set_on_the_company()
    {
        var company = eiti.Domain.Companies.Company.CreateLegacy(_companyId);
        var companies = new Mock<ICompanyRepository>();
        companies.Setup(x => x.GetByIdAsync(_companyId, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var result = await new eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing.SetAutomaticInvoicingHandler(
                _currentUser.Object, companies.Object, _unitOfWork.Object)
            .Handle(new eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing.SetAutomaticInvoicingCommand(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        company.AutomaticInvoicing.Should().BeTrue();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void The_issuer_cannot_be_a_final_consumer_nor_start_in_the_future()
    {
        var validator = new UpdateFiscalIssuerValidator();

        validator.Validate(Issuer(IvaCondition.ResponsableInscripto, new DateOnly(2015, 3, 1))).IsValid.Should().BeTrue();
        validator.Validate(Issuer(IvaCondition.ConsumidorFinal, new DateOnly(2015, 3, 1))).IsValid.Should().BeFalse();
        validator.Validate(Issuer(IvaCondition.Monotributo, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task The_client_reads_the_issuer_as_the_fiscal_service_returns_it()
    {
        const string body = """
            {"id":"6f0f3a8c-1d1e-4e55-9d65-6f2f2b7b0c11","cuit":"20397583857","legalName":"Soler Emiliano","vatCondition":"registered",
             "iibb":"901-123456-7","activityStartDate":"2015-03-01","commercialAddress":"Av. Rivadavia 1000","environment":"production",
             "pointsOfSale":[1,3],"certificateNotAfter":"2028-05-01T00:00:00+00:00"}
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);

        var result = await Client(handler).GetIssuerAsync(Guid.NewGuid());

        result.Issuer.Should().BeEquivalentTo(new
        {
            Cuit = "20397583857",
            VatCondition = IvaCondition.ResponsableInscripto,
            ActivityStartDate = new DateOnly(2015, 3, 1),
            CommercialAddress = "Av. Rivadavia 1000",
            IsProduction = true,
            PointsOfSale = new[] { 1, 3 }
        });
    }

    [Fact]
    public async Task The_client_sends_the_vat_condition_in_the_fiscal_service_vocabulary()
    {
        const string response = """
            {"cuit":"20397583857","legalName":"Soler","vatCondition":"monotribute","iibb":null,"activityStartDate":"2015-03-01",
             "commercialAddress":null,"environment":"certification","pointsOfSale":[],"certificateNotAfter":null}
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, response);

        var result = await Client(handler).UpdateIssuerAsync(Guid.NewGuid(),
            new FiscalIssuerUpdate("Soler", IvaCondition.Monotributo, null, new DateOnly(2015, 3, 1), null));

        result.IsSuccess.Should().BeTrue();
        handler.Method.Should().Be(HttpMethod.Put);
        handler.Path.Should().EndWith("/api/admin/fiscal-profiles/active/issuer");
        handler.Body.Should().Contain("\"vatCondition\":\"monotribute\"").And.Contain("\"activityStartDate\":\"2015-03-01\"");
    }

    [Fact]
    public async Task Without_an_active_profile_the_reason_reaches_the_screen_in_spanish()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound, """{"code":"FiscalProfile.NotFound","description":"The tenant has no active fiscal profile.","type":2}""");

        var result = await Client(handler).GetIssuerAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FiscalProfile.NotFound");
        result.ErrorMessage.Should().Contain("todavía no tiene un perfil fiscal activo");
    }

    private Task<eiti.Application.Common.Result<eiti.Application.Features.FiscalSettings.Common.FiscalPointOfSaleResponse>> Create(int number, Guid? branchId) =>
        new CreateFiscalPointOfSaleHandler(_currentUser.Object, _pointsOfSale.Object, _branches.Object, _fiscal.Object, _unitOfWork.Object)
            .Handle(new CreateFiscalPointOfSaleCommand(number, branchId), CancellationToken.None);

    private Task<eiti.Application.Common.Result<eiti.Application.Features.FiscalSettings.Common.FiscalPointOfSaleResponse>> Assign(Guid pointOfSaleId, Guid? branchId) =>
        new AssignFiscalPointOfSaleHandler(_currentUser.Object, _pointsOfSale.Object, _branches.Object, _unitOfWork.Object)
            .Handle(new AssignFiscalPointOfSaleCommand(pointOfSaleId, branchId), CancellationToken.None);

    private Task<eiti.Application.Common.Result> Delete(Guid pointOfSaleId) =>
        new DeleteFiscalPointOfSaleHandler(_currentUser.Object, _pointsOfSale.Object, _branches.Object, _unitOfWork.Object)
            .Handle(new DeleteFiscalPointOfSaleCommand(pointOfSaleId), CancellationToken.None);

    private static UpdateFiscalIssuerCommand Issuer(IvaCondition condition, DateOnly start) =>
        new("Soler Emiliano", condition, "901-123456-7", start, "Av. Rivadavia 1000");

    private static FiscalizationService Client(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new FiscalizationOptions { BaseUrl = "http://fiscal.local", ApiKey = "test-key" }),
        NullLogger<FiscalizationService>.Instance);

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string Path { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
