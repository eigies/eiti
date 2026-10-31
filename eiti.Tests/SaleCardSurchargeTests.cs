using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateSale;
using eiti.Application.Features.Sales.Commands.UpdateSale;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

/// <summary>
/// El recargo de la tarjeta se calcula sobre lo que paga esa tarjeta (la base que manda el front), no sobre
/// toda la venta: con pago mixto el cobro cubre exactamente la venta más el recargo y no queda saldo pendiente.
/// </summary>
public sealed class SaleCardSurchargeTests
{
    private const decimal MouraPct = 11.06m;

    private readonly CompanyId _companyId = CompanyId.New();
    private readonly UserId _userId = UserId.New();
    private readonly CashDrawerId _drawerId = CashDrawerId.New();
    private readonly Branch _branch;
    private readonly Product _battery;
    private readonly BranchProductStock _stock;
    private readonly Bank _moura;
    private readonly CashSession _session;

    public SaleCardSurchargeTests()
    {
        _branch = Branch.Create(_companyId, "Mataderos", "MAT", "Av. Juan B. Alberdi 5000");
        _battery = Product.Create(_companyId, "22GD", "22GD", "MOURA", "Bateria 12x65", null, 100000m, 70000m, null);
        _stock = BranchProductStock.Create(_companyId, _branch.Id, _battery.Id);
        _stock.ApplyManualEntry(10);
        _moura = Bank.Create(_companyId, "MOURA");
        _moura.UpsertInstallmentPlan(3, MouraPct, active: true);
        _moura.UpsertInstallmentPlan(1, 0m, active: true);
        _session = CashSession.Open(_companyId, _branch.Id, _drawerId, _userId, 0m, null);
    }

    // Ejemplo del spec: $100.000 → $40.000 efectivo + $60.000 con MOURA 3 cuotas 11,06 %.
    [Fact]
    public async Task Create_charges_the_surcharge_only_on_the_card_base_and_leaves_nothing_pending()
    {
        Sale? persisted = null;

        var result = await CreateHandler(sale => persisted = sale).Handle(
            CreateCommand(
                SaleStatus.Paid,
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Cash, 40000m, null),
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 60000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        persisted!.SaleStatus.Should().Be(SaleStatus.Paid);
        persisted.TotalAmount.Should().Be(100000m);
        persisted.CardSurchargeTotal.Should().Be(6636m);
        persisted.EffectiveTotal.Should().Be(106636m);
        persisted.PendingAmount.Should().Be(0m);
        persisted.ChangeAmount.Should().Be(0m);

        var card = persisted.Payments.Single(p => p.Method == SalePaymentMethod.Card);
        card.Amount.Should().Be(66636m, "lo que pasa por el posnet es la base más su recargo");
        card.CardSurchargeAmt.Should().Be(6636m);
        card.CardSurchargePct.Should().Be(MouraPct);
        card.CardBankId.Should().Be(_moura.Id);
        card.CardCuotas.Should().Be(3);
        persisted.Payments.Single(p => p.Method == SalePaymentMethod.Cash).Amount.Should().Be(40000m);
    }

    [Fact]
    public async Task Create_rejects_more_than_one_card_line_instead_of_merging_them()
    {
        var sales = new Mock<ISaleRepository>();

        var result = await CreateHandler(sales: sales).Handle(
            CreateCommand(
                SaleStatus.Paid,
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 40000m, null, CardBankId: _moura.Id, CardCuotas: 1),
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 60000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Payments.SingleCard");
        result.Error.Type.Should().Be(eiti.Application.Common.ErrorType.Validation);
        sales.Verify(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
        _stock.ReservedQuantity.Should().Be(0);
    }

    [Theory]
    [InlineData(1)] // plan al 0 %
    [InlineData(6)] // el banco no tiene plan de 6 cuotas
    public async Task Create_without_a_surcharge_plan_charges_just_the_base(int cuotas)
    {
        Sale? persisted = null;

        var result = await CreateHandler(sale => persisted = sale).Handle(
            CreateCommand(
                SaleStatus.Paid,
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 100000m, null, CardBankId: _moura.Id, CardCuotas: cuotas)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        persisted!.CardSurchargeTotal.Should().Be(0m);
        persisted.Payments.Single().Amount.Should().Be(100000m);
        persisted.PendingAmount.Should().Be(0m);
    }

    // Venta en espera cobrada en parte con tarjeta: la venta guarda el recargo de esa parte y el saldo
    // pendiente es exactamente lo que falta de la mercadería.
    [Fact]
    public async Task Create_on_hold_with_a_partial_card_payment_keeps_the_rest_pending()
    {
        Sale? persisted = null;

        var result = await CreateHandler(sale => persisted = sale).Handle(
            CreateCommand(
                SaleStatus.OnHold,
                new CreateSalePaymentItemRequest((int)SalePaymentMethod.Card, 60000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        persisted!.CardSurchargeTotal.Should().Be(6636m);
        persisted.Payments.Single().Amount.Should().Be(66636m);
        persisted.PendingAmount.Should().Be(40000m);
    }

    [Fact]
    public async Task Update_charging_in_installments_charges_the_surcharge_on_the_card_base()
    {
        var sale = OnHoldSale();

        var result = await UpdateHandler(sale).Handle(
            UpdateCommand(
                sale,
                SaleStatus.Paid,
                new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Cash, 40000m, null),
                new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 60000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        sale.SaleStatus.Should().Be(SaleStatus.Paid);
        sale.CardSurchargeTotal.Should().Be(6636m);
        sale.PendingAmount.Should().Be(0m);
        var card = sale.Payments.Single(p => p.Method == SalePaymentMethod.Card);
        card.Amount.Should().Be(66636m);
        card.CardSurchargeAmt.Should().Be(6636m);
        card.CardSurchargePct.Should().Be(MouraPct);
    }

    [Fact]
    public async Task Update_rejects_more_than_one_card_line()
    {
        var sale = OnHoldSale();
        var unitOfWork = new Mock<IUnitOfWork>();

        var result = await UpdateHandler(sale, unitOfWork: unitOfWork).Handle(
            UpdateCommand(
                sale,
                SaleStatus.Paid,
                new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 40000m, null, CardBankId: _moura.Id, CardCuotas: 1),
                new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 60000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.Payments.SingleCard");
        sale.SaleStatus.Should().Be(SaleStatus.OnHold);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Opción (a): una venta facturada en espera se puede cobrar después en cuotas. La factura no cambia
    // (la mercadería es la misma); el recargo se cobra y queda como "no facturado".
    [Fact]
    public async Task Update_accepts_charging_an_invoiced_sale_in_installments()
    {
        var sale = OnHoldSale();
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, sale.Id, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceB", 10, 1, "86428447195933", DateTime.UtcNow.AddDays(10), "https://qr", DateTime.UtcNow);

        var result = await UpdateHandler(sale, [invoice]).Handle(
            UpdateCommand(
                sale,
                SaleStatus.Paid,
                new UpdateSalePaymentItemRequest((int)SalePaymentMethod.Card, 100000m, null, CardBankId: _moura.Id, CardCuotas: 3)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        sale.SaleStatus.Should().Be(SaleStatus.Paid);
        sale.TotalAmount.Should().Be(100000m);
        sale.CardSurchargeTotal.Should().Be(11060m);
    }

    // Anular por la edición conserva los pagos tal cual: el recargo de la venta tiene que seguir siendo el
    // que traen (antes volvía a 0 y la venta anulada quedaba con un pago con recargo y sin recargo propio).
    [Fact]
    public async Task Cancelling_through_update_keeps_the_card_surcharge_of_the_kept_payments()
    {
        var card = SaleCardPayment.Charge(_moura, 3, 60000m).CreatePayment(null);
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)], [card], cardSurchargeTotal: 6636m);
        _stock.Reserve(1);

        var result = await UpdateHandler(sale).Handle(UpdateCommand(sale, SaleStatus.Cancel), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        sale.SaleStatus.Should().Be(SaleStatus.Cancel);
        sale.CardSurchargeTotal.Should().Be(6636m);
    }

    private CreateSaleCommand CreateCommand(SaleStatus status, params CreateSalePaymentItemRequest[] payments) =>
        new(_branch.Id.Value, null, (int)status, false, _drawerId.Value,
            [new CreateSaleDetailItemRequest(_battery.Id.Value, 1)], payments, []);

    private UpdateSaleCommand UpdateCommand(Sale sale, SaleStatus status, params UpdateSalePaymentItemRequest[] payments) =>
        new(sale.Id.Value, null, (int)status, false, _drawerId.Value,
            [new UpdateSaleDetailItemRequest(_battery.Id.Value, 1)], payments, []);

    private Sale OnHoldSale()
    {
        var sale = Sale.Create(_companyId, _branch.Id, null, false, SaleStatus.OnHold,
            [SaleDetail.Create(_battery.Id, 1, _battery.Price)]);
        _stock.Reserve(1);
        return sale;
    }

    private Mock<ICurrentUserService> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        currentUser.SetupGet(x => x.UserId).Returns(_userId);
        currentUser.Setup(x => x.HasPermission(PermissionCodes.SalesPay)).Returns(true);
        return currentUser;
    }

    private Mock<ICashSessionRepository> CashSessions()
    {
        var sessions = new Mock<ICashSessionRepository>();
        sessions.Setup(x => x.GetOpenForBranchAsync(_branch.Id, _drawerId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);
        return sessions;
    }

    private CreateSaleHandler CreateHandler(Action<Sale>? onAdd = null, Mock<ISaleRepository>? sales = null)
    {
        var branches = new Mock<IBranchRepository>();
        branches.Setup(x => x.GetByIdAsync(_branch.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_branch);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<ProductId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_battery]);
        var stocks = new Mock<IBranchProductStockRepository>();
        stocks.Setup(x => x.GetOrCreateAsync(_branch.Id, _battery.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_stock);
        sales ??= new Mock<ISaleRepository>();
        sales.Setup(x => x.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((sale, _) => onAdd?.Invoke(sale))
            .Returns(Task.CompletedTask);
        var banks = new Mock<IBankRepository>();
        banks.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_moura]);

        return new CreateSaleHandler(
            CurrentUser().Object, branches.Object, new Mock<ICustomerRepository>().Object, products.Object,
            stocks.Object, new Mock<IStockMovementRepository>().Object, sales.Object,
            new Mock<ICashDrawerRepository>().Object, CashSessions().Object, new Mock<IAddressRepository>().Object,
            banks.Object, new Mock<IChequeRepository>().Object, new Mock<ISaleInvoicingService>().Object,
            new RetailDiscountPolicy(new Mock<ICompanyRepository>().Object), new Mock<IUnitOfWork>().Object);
    }

    private UpdateSaleHandler UpdateHandler(
        Sale sale,
        IReadOnlyList<SaleFiscalDocument>? fiscalDocuments = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        var sales = new Mock<ISaleRepository>();
        sales.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdAsync(_battery.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_battery);
        var stocks = new Mock<IBranchProductStockRepository>();
        stocks.Setup(x => x.GetOrCreateAsync(_branch.Id, _battery.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_stock);
        var banks = new Mock<IBankRepository>();
        banks.Setup(x => x.GetByIdAsync(_moura.Id, _companyId, It.IsAny<CancellationToken>())).ReturnsAsync(_moura);
        var documents = new Mock<ISaleFiscalDocumentRepository>();
        documents.Setup(x => x.ListBySaleAsync(sale.Id, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiscalDocuments ?? []);
        var creditNotes = new Mock<ICustomerCreditNoteRepository>();
        creditNotes.Setup(x => x.ListBySaleAsync(_companyId.Value, sale.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new UpdateSaleHandler(
            CurrentUser().Object, sales.Object, new Mock<ICustomerRepository>().Object, products.Object,
            stocks.Object, new Mock<IStockMovementRepository>().Object, new Mock<ICashDrawerRepository>().Object,
            CashSessions().Object, new Mock<ISaleTransportAssignmentRepository>().Object, new Mock<IAddressRepository>().Object,
            banks.Object, documents.Object, creditNotes.Object,
            new RetailDiscountPolicy(new Mock<ICompanyRepository>().Object), (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }
}
