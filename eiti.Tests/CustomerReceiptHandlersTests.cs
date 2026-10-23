using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.CashSessions.Queries.GetCashSessionSummary;
using eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;
using eiti.Application.Features.Customers.Commands.AddCustomerReceipt;
using eiti.Application.Features.Customers.Commands.CancelCustomerPayment;
using eiti.Application.Features.Customers.Commands.CancelCustomerReceipt;
using eiti.Application.Features.Customers.Common;
using eiti.Application.Features.Customers.Queries.GetCustomerLedger;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class CustomerReceiptHandlersTests
{
    private readonly CompanyId _companyId = CompanyId.New();
    private readonly UserId _userId = UserId.New();
    private readonly Branch _branch;
    private readonly CashSession _session;
    private readonly Customer _customer;
    private readonly List<Sale> _pending = [];
    private readonly List<CustomerReceipt> _receipts = [];
    private readonly List<CustomerPayment> _payments = [];
    private readonly List<Cheque> _cheques = [];
    private readonly List<SaleFiscalDocument> _fiscalDocuments = [];
    private readonly Mock<IStockMovementRepository> _stockMovements = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICustomerPaymentRepository> _paymentRepository = new();
    private readonly Mock<ICustomerReceiptRepository> _receiptRepository = new();
    private readonly BranchProductStock _stock;
    private readonly ProductId _productId = ProductId.New();
    private Bank _bank;
    private CustomerReceiptService _service = null!; // lo arma Handler(); los handlers de anulación lo reusan.
    private (Mock<ICurrentUserService> User, Mock<ICustomerRepository> Customers, Mock<ICashDrawerRepository> Drawers, Mock<ICashSessionRepository> Sessions) _context;

    public CustomerReceiptHandlersTests()
    {
        _branch = Branch.Create(_companyId, "Sucursal Centro", "SC", "San Martin 123");
        var drawer = CashDrawer.Create(_companyId, _branch.Id, "Caja 1");
        _session = CashSession.Open(_companyId, _branch.Id, drawer.Id, _userId, 0m, null);
        _customer = Customer.Create(_companyId, "Juan", "Perez", null);

        _bank = Bank.Create(_companyId, "Banco Nación", useForCard: true, useForTransfer: true, useForCheque: true);
        _bank.UpsertInstallmentPlan(3, 10m, true);

        _stock = BranchProductStock.Create(_companyId, _branch.Id, _productId);
        _stock.ApplyManualEntry(5);
        _stock.Reserve(1);
    }

    [Fact]
    public async Task VariosMedios_UnRecibo_RecargoDeTarjeta_ChequeACartera_StockConfirmadoUnaVez()
    {
        var vieja = AddCcSale("VENT-0101", 300_000m, withStock: true);
        var nueva = AddCcSale("VENT-0102", 100_000m);

        var result = await Handler(existingReceipts: 7).Handle(
            new AddCustomerReceiptCommand(
                _customer.Id.Value,
                new DateTime(2026, 10, 2),
                "Cobro octubre",
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Card, 200_000m, null, CardBankId: _bank.Id, CardCuotas: 3),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Check, 100_000m, null, Cheque: new CustomerPaymentChequeInput(
                        _bank.Id, "00045120", "Ferretería Sur", "30712345678", 100_000m,
                        new DateTime(2026, 10, 1), new DateTime(2026, 11, 10), null)),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 25_000m, "Caja 1"),
                ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value;
        response.Code.Should().Be("RC-0008");
        response.TotalAmount.Should().Be(325_000m);

        var receipt = _receipts.Single();
        _payments.Should().HaveCount(3).And.OnlyContain(p => p.ReceiptId == receipt.Id);

        var card = _payments.Single(p => p.Method == SalePaymentMethod.Card);
        card.CardSurchargeAmt.Should().Be(20_000m);
        card.TotalCobrado.Should().Be(220_000m);
        card.Amount.Should().Be(200_000m, "el recargo no altera lo imputado a la cuenta");

        var chequePayment = _payments.Single(p => p.Method == SalePaymentMethod.Check);
        var cheque = _cheques.Single();
        chequePayment.ChequeId.Should().Be(cheque.Id);
        cheque.Estado.Should().Be(ChequeStatus.EnCartera);

        _session.ExpectedClosingAmount.Should().Be(25_000m, "tarjeta y cheque no entran al cajón");

        vieja.CcPendingAmount.Should().Be(0m);
        nueva.CcPendingAmount.Should().Be(75_000m);
        response.Imputaciones.Select(i => (i.Code, i.Amount)).Should().Equal(
            ("VENT-0101", 300_000m),
            ("VENT-0102", 25_000m));

        // La venta vieja se terminó de pagar entre la tarjeta y el cheque: su stock se confirma una sola vez.
        _stockMovements.Verify(r => r.AddAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()), Times.Once);
        _stock.ReservedQuantity.Should().Be(0);
        _stock.OnHandQuantity.Should().Be(4);

        _customer.CreditBalance.Should().Be(0m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnMedioInvalido_NoRegistraNada()
    {
        AddCcSale("VENT-0101", 300_000m);
        _bank = Bank.Create(_companyId, "Banco sin tarjeta", useForCard: false, useForTransfer: true, useForCheque: true);

        var result = await Handler().Handle(
            new AddCustomerReceiptCommand(
                _customer.Id.Value,
                new DateTime(2026, 10, 2),
                null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 25_000m, null),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Card, 200_000m, null, CardBankId: _bank.Id, CardCuotas: 1),
                ]),
            CancellationToken.None);

        result.Error.Should().Be(CustomerPaymentLineErrors.CardBankInvalid);
        _receipts.Should().BeEmpty();
        _payments.Should().BeEmpty();
        _cheques.Should().BeEmpty();
        _session.Movements.Should().BeEmpty();
        _customer.CreditBalance.Should().Be(0m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Validador_RechazaSinMedios_SaldoAFavorComoMedioYChequeSinDatos()
    {
        var validator = new AddCustomerReceiptValidator();
        var customerId = Guid.NewGuid();

        validator.Validate(new AddCustomerReceiptCommand(customerId, DateTime.Today, null, []))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddCustomerReceiptCommand(customerId, DateTime.Today, null,
                [new AddCustomerReceiptLine((int)SalePaymentMethod.CustomerCredit, 1m, null)]))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddCustomerReceiptCommand(customerId, DateTime.Today, null,
                [new AddCustomerReceiptLine((int)SalePaymentMethod.Check, 1m, null)]))
            .IsValid.Should().BeFalse();
        validator.Validate(new AddCustomerReceiptCommand(customerId, DateTime.Today, null,
                [new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 1m, null),
                 new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 1m, "Ref. 1")]))
            .IsValid.Should().BeTrue();
    }

    private Sale AddCcSale(string code, decimal total, bool withStock = false)
    {
        var sale = Sale.CreateCc(
            _companyId,
            _branch.Id,
            _customer.Id,
            [SaleDetail.Create(withStock ? _productId : ProductId.New(), 1, total)],
            code: code);
        _pending.Add(sale);
        return sale;
    }

    private AddCustomerReceiptHandler Handler(int existingReceipts = 0)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.CompanyId).Returns(_companyId);
        currentUser.SetupGet(x => x.UserId).Returns(_userId);

        var customers = new Mock<ICustomerRepository>();
        customers
            .Setup(r => r.GetByIdAsync(_customer.Id, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_customer);

        var drawers = new Mock<ICashDrawerRepository>();
        drawers
            .Setup(r => r.GetByAssignedUserAsync(_userId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashDrawer.Create(_companyId, _branch.Id, "Caja 1"));

        var sessions = new Mock<ICashSessionRepository>();
        sessions
            .Setup(r => r.GetOpenByDrawerAsync(It.IsAny<CashDrawerId>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);

        _paymentRepository
            .Setup(r => r.AddAsync(It.IsAny<CustomerPayment>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerPayment, CancellationToken>((p, _) => _payments.Add(p))
            .Returns(Task.CompletedTask);
        _paymentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _payments.FirstOrDefault(p => p.Id == id));
        _paymentRepository
            .Setup(r => r.ListByReceiptAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid receiptId, Guid _, CancellationToken _) => _payments.Where(p => p.ReceiptId == receiptId).ToList());

        _receiptRepository
            .Setup(r => r.CountByCompanyAsync(_companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existingReceipts + _receipts.Count);
        _receiptRepository
            .Setup(r => r.AddAsync(It.IsAny<CustomerReceipt>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerReceipt, CancellationToken>((r, _) => _receipts.Add(r))
            .Returns(Task.CompletedTask);
        _receiptRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _receipts.FirstOrDefault(r => r.Id == id));

        var cheques = new Mock<IChequeRepository>();
        cheques
            .Setup(r => r.AddAsync(It.IsAny<Cheque>(), It.IsAny<CancellationToken>()))
            .Callback<Cheque, CancellationToken>((c, _) => _cheques.Add(c))
            .Returns(Task.CompletedTask);

        var sales = new Mock<ISaleRepository>();
        sales
            .Setup(r => r.ListPendingCcSalesByCustomerAsync(_companyId, _customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());
        sales
            .Setup(r => r.ListByCustomerPaymentIdAsync(_companyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyId _, Guid paymentId, CancellationToken _) =>
                _pending.Where(sale => sale.CcPayments.Any(x => x.CustomerPaymentId == paymentId)).ToList());
        cheques
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CompanyId _, CancellationToken _) => _cheques.FirstOrDefault(c => c.Id == id));

        var banks = new Mock<IBankRepository>();
        banks
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _bank);

        var stocks = new Mock<IBranchProductStockRepository>();
        stocks
            .Setup(r => r.GetOrCreateAsync(_branch.Id, _productId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_stock);

        _service = new CustomerReceiptService(
            customers.Object,
            _paymentRepository.Object,
            _receiptRepository.Object,
            sales.Object,
            banks.Object,
            cheques.Object,
            stocks.Object,
            _stockMovements.Object);
        _context = (currentUser, customers, drawers, sessions);

        return new AddCustomerReceiptHandler(
            currentUser.Object, customers.Object, drawers.Object, sessions.Object, _service, _unitOfWork.Object);
    }

    // ── Anulación ────────────────────────────────────────────────────────────

    private CancelCustomerReceiptHandler CancelReceiptHandler() =>
        new(_context.User.Object, _context.Customers.Object, _receiptRepository.Object, _paymentRepository.Object,
            _context.Drawers.Object, _context.Sessions.Object, _service, _unitOfWork.Object);

    private CancelCustomerPaymentHandler CancelPaymentHandler() =>
        new(_context.User.Object, _context.Customers.Object, _paymentRepository.Object,
            _context.Drawers.Object, _context.Sessions.Object, _service, _unitOfWork.Object);

    private async Task<CustomerReceipt> CollectWithThreeMeansAsync()
    {
        var result = await Handler().Handle(
            new AddCustomerReceiptCommand(
                _customer.Id.Value,
                new DateTime(2026, 10, 2),
                null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Card, 200_000m, null, CardBankId: _bank.Id, CardCuotas: 3),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Check, 100_000m, null, Cheque: new CustomerPaymentChequeInput(
                        _bank.Id, "00045120", "Ferretería Sur", "30712345678", 100_000m,
                        new DateTime(2026, 10, 1), new DateTime(2026, 11, 10), null)),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 25_000m, null),
                ]),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return _receipts.Single();
    }

    [Fact]
    public async Task AnularRecibo_RevierteTodosLosMedios_VentasStockChequeYCaja()
    {
        var vieja = AddCcSale("VENT-0101", 300_000m, withStock: true);
        var nueva = AddCcSale("VENT-0102", 100_000m);
        var receipt = await CollectWithThreeMeansAsync();
        _stock.ReservedQuantity.Should().Be(0);

        var result = await CancelReceiptHandler().Handle(
            new CancelCustomerReceiptCommand(_customer.Id.Value, receipt.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        receipt.Status.Should().Be(SaleCcPaymentStatus.Cancelled);
        _payments.Should().OnlyContain(p => p.Status == SaleCcPaymentStatus.Cancelled);
        vieja.CcPendingAmount.Should().Be(300_000m);
        nueva.CcPendingAmount.Should().Be(100_000m);
        _stock.ReservedQuantity.Should().Be(1, "la venta deja de estar paga y su stock vuelve a reservado");
        _stock.OnHandQuantity.Should().Be(5);
        _cheques.Single().Estado.Should().Be(ChequeStatus.Anulado);
        _session.ExpectedClosingAmount.Should().Be(0m);
        _customer.CreditBalance.Should().Be(0m);
    }

    [Fact]
    public async Task AnularUnCobroSuelto_DeUnReciboConVariosMedios_SeRechaza()
    {
        AddCcSale("VENT-0101", 300_000m, withStock: true);
        var receipt = await CollectWithThreeMeansAsync();
        var cash = _payments.Single(p => p.Method == SalePaymentMethod.Cash);

        var result = await CancelPaymentHandler().Handle(
            new CancelCustomerPaymentCommand(_customer.Id.Value, cash.Id), CancellationToken.None);

        result.Error.Should().Be(CancelCustomerPaymentErrors.PartOfReceipt);
        cash.Status.Should().Be(SaleCcPaymentStatus.Active);
        receipt.Status.Should().Be(SaleCcPaymentStatus.Active);
    }

    [Fact]
    public async Task AnularCobroDeUnSoloMedio_AnulaTambienSuRecibo()
    {
        var venta = AddCcSale("VENT-0101", 300_000m);
        (await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 50_000m, "Ref. 1")]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        var payment = _payments.Single();

        var result = await CancelPaymentHandler().Handle(
            new CancelCustomerPaymentCommand(_customer.Id.Value, payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(SaleCcPaymentStatus.Cancelled);
        _receipts.Single().Status.Should().Be(SaleCcPaymentStatus.Cancelled);
        venta.CcPendingAmount.Should().Be(300_000m);
    }

    // La venta CC se factura igual que la de mostrador: en el extracto su comprobante es la factura con CAE,
    // como el Nº de factura del proveedor en las compras. Una factura rechazada no es comprobante.
    [Fact]
    public async Task Extracto_UnaVentaFacturada_MuestraSuFacturaComoComprobante()
    {
        var facturada = AddCcSale("VENT-0101", 300_000m);
        var rechazada = AddCcSale("VENT-0102", 100_000m);
        var invoice = SaleFiscalDocument.CreateInvoice(_companyId, facturada.Id, 1);
        invoice.Authorize(Guid.NewGuid(), "invoiceB", 3, 1045, "75123", new DateTime(2026, 11, 2), "https://qr", DateTime.UtcNow);
        var rejected = SaleFiscalDocument.CreateInvoice(_companyId, rechazada.Id, 1);
        rejected.Reject(null, "CUIT inválido");
        _fiscalDocuments.AddRange([invoice, rejected]);
        Handler(); // arma el contexto que reusa el extracto

        var ledger = (await LedgerHandler().Handle(new GetCustomerLedgerQuery(_customer.Id.Value), CancellationToken.None)).Value;

        ledger.Rows.Single(r => r.Code == "VENT-0101").Document.Should().Be("B 00003-00001045");
        ledger.Rows.Single(r => r.Code == "VENT-0102").Document.Should().BeNull();
        ledger.Pending.Single(p => p.Code == "VENT-0101").Document.Should().Be("B 00003-00001045");
    }

    // ── Saldo a favor previo ─────────────────────────────────────────────────
    // Al registrar un cobro se imputa TODO el saldo disponible con back-link al cobro, también el que el
    // cliente ya tenía. Anularlo tiene que dejar la cuenta como estaba: venta pendiente y saldo previo.

    [Fact]
    public async Task AnularCobro_QueImputoSaldoAFavorPrevio_DevuelveEseSaldo()
    {
        var venta = AddCcSale("VENT-0101", 100_000m);
        _customer.AddCredit(50_000m); // saldo previo conviviendo con la venta pendiente
        (await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 30_000m, "Ref. 1")]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        venta.CcPendingAmount.Should().Be(20_000m, "el cobro y el saldo previo se imputan juntos");
        _customer.CreditBalance.Should().Be(0m);

        var result = await CancelPaymentHandler().Handle(
            new CancelCustomerPaymentCommand(_customer.Id.Value, _payments.Single().Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        venta.CcPendingAmount.Should().Be(100_000m);
        _customer.CreditBalance.Should().Be(50_000m, "ese saldo no lo aportó el cobro anulado");
    }

    [Fact]
    public async Task AnularRecibo_QueImputoSaldoAFavorPrevio_DevuelveEseSaldo()
    {
        var venta = AddCcSale("VENT-0101", 200_000m);
        _customer.AddCredit(50_000m);
        (await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 30_000m, "Ref. 1"),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 20_000m, null),
                ]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        venta.CcPendingAmount.Should().Be(100_000m);

        var result = await CancelReceiptHandler().Handle(
            new CancelCustomerReceiptCommand(_customer.Id.Value, _receipts.Single().Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        venta.CcPendingAmount.Should().Be(200_000m);
        _customer.CreditBalance.Should().Be(50_000m);
    }

    // ── Extracto ─────────────────────────────────────────────────────────────

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private GetCustomerLedgerHandler LedgerHandler()
    {
        var sales = new Mock<ISaleRepository>();
        sales
            .Setup(r => r.ListCcSalesByCustomerAsync(_companyId, _customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());
        _paymentRepository
            .Setup(r => r.ListByCustomerAsync(_companyId.Value, _customer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _payments.ToList());
        _receiptRepository
            .Setup(r => r.ListByCustomerAsync(_companyId.Value, _customer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _receipts.ToList());
        var creditNotes = new Mock<ICustomerCreditNoteRepository>();
        creditNotes
            .Setup(r => r.ListByCustomerAsync(_companyId.Value, _customer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var cheques = new Mock<IChequeRepository>();
        cheques
            .Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _cheques.ToList());
        var banks = new Mock<IBankRepository>();
        banks
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [_bank]);

        var fiscalDocuments = new Mock<ISaleFiscalDocumentRepository>();
        fiscalDocuments
            .Setup(r => r.ListBySaleIdsAsync(It.IsAny<IReadOnlyCollection<SaleId>>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<SaleId> ids, CompanyId _, CancellationToken _) =>
                _fiscalDocuments.Where(d => ids.Contains(d.SaleId)).ToList());

        return new GetCustomerLedgerHandler(
            _context.User.Object, _context.Customers.Object, sales.Object, _paymentRepository.Object,
            _receiptRepository.Object, creditNotes.Object, cheques.Object, banks.Object, fiscalDocuments.Object,
            new FixedTime(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Extracto_ElReciboEsUnRenglonConSusMedios_YCuadraConLasTarjetas()
    {
        AddCcSale("VENT-0101", 300_000m, withStock: true);
        AddCcSale("VENT-0102", 100_000m);
        var receipt = await CollectWithThreeMeansAsync();

        var result = await LedgerHandler().Handle(new GetCustomerLedgerQuery(_customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ledger = result.Value;
        ledger.ClosingBalance.Should().Be(_pending.Sum(s => s.CcPendingAmount) - _customer.CreditBalance);
        ledger.ClosingBalance.Should().Be(75_000m);

        var row = ledger.Rows.Single(r => r.Source == "receipt");
        row.Id.Should().Be(receipt.Id);
        row.Code.Should().Be("RC-0001");
        row.Credit.Should().Be(325_000m);
        ledger.Rows.Select(r => r.Date).Should().BeInAscendingOrder();
        ledger.Rows.Last().Balance.Should().Be(75_000m);

        var card = row.Means.Single(m => m.Method == "card");
        card.BankName.Should().Be("Banco Nación");
        card.CardInstallments.Should().Be(3);
        card.CardSurcharge.Should().Be(20_000m);
        row.Means.Single(m => m.Method == "check").ChequeNumber.Should().Be("00045120");
        row.Imputations.Select(i => (i.Code, i.Amount)).Should().Equal(("VENT-0101", 300_000m), ("VENT-0102", 25_000m));

        ledger.Pending.Should().ContainSingle();
        ledger.Pending[0].Code.Should().Be("VENT-0102");
        ledger.Pending[0].Imputed.Should().Be(25_000m);
        ledger.Pending[0].Pending.Should().Be(75_000m);
    }

    // ── Banco de la transferencia ────────────────────────────────────────────
    // Antes el front lo mandaba y el cobro no tenía dónde guardarlo: el cierre de caja nunca veía las
    // transferencias de cuenta corriente en su desglose por banco.

    [Fact]
    public async Task Transferencia_GuardaElBancoQueLaRecibio_YElExtractoLoMuestra()
    {
        AddCcSale("VENT-0101", 300_000m);

        (await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 50_000m, "Ref. 88213", TransferBankId: _bank.Id),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 10_000m, null, TransferBankId: _bank.Id),
                ]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();

        _payments.Single(p => p.Method == SalePaymentMethod.Transfer).TransferBankId.Should().Be(_bank.Id);
        _payments.Single(p => p.Method == SalePaymentMethod.Cash).TransferBankId
            .Should().BeNull("el banco receptor es solo de las transferencias");

        var ledger = (await LedgerHandler().Handle(new GetCustomerLedgerQuery(_customer.Id.Value), CancellationToken.None)).Value;
        var transfer = ledger.Rows.Single(r => r.Source == "receipt").Means.Single(m => m.Method == "transfer");
        transfer.BankName.Should().Be("Banco Nación");
        transfer.Reference.Should().Be("Ref. 88213");
    }

    [Fact]
    public async Task Transferencia_AUnBancoNoHabilitado_NoRegistraNada()
    {
        AddCcSale("VENT-0101", 300_000m);
        _bank = Bank.Create(_companyId, "Solo tarjetas", useForCard: true, useForTransfer: false, useForCheque: false);

        var result = await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 10_000m, null),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 50_000m, null, TransferBankId: _bank.Id),
                ]),
            CancellationToken.None);

        result.Error.Should().Be(CustomerPaymentLineErrors.TransferBankInvalid);
        _payments.Should().BeEmpty();
        _receipts.Should().BeEmpty();
    }

    // La caja que ve el usuario: el resumen (desglose por banco) y la sesión actual (movimientos).
    private (GetCashSessionSummaryHandler Summary, GetCurrentCashSessionHandler Current) CashHandlers()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.CompanyId).Returns(_companyId);
        user.SetupGet(x => x.UserId).Returns(_userId);
        user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var sessions = new Mock<ICashSessionRepository>();
        sessions
            .Setup(r => r.GetByIdAsync(_session.Id, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);
        sessions
            .Setup(r => r.GetOpenByDrawerAsync(_session.CashDrawerId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);

        var sales = new Mock<ISaleRepository>();
        sales
            .Setup(r => r.GetPaymentsByCashSessionIdAsync(_session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var banks = new Mock<IBankRepository>();
        banks
            .Setup(r => r.ListAsync(false, _companyId, It.IsAny<CancellationToken>(), BankUsage.All))
            .ReturnsAsync(() => [_bank]);
        var users = new Mock<IUserRepository>();
        users
            .Setup(r => r.GetUsernamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Lo que resuelve la base: el banco receptor de cada cobro por transferencia pedido.
        var payments = new Mock<ICustomerPaymentRepository>();
        payments
            .Setup(r => r.GetTransferBanksAsync(It.IsAny<IReadOnlyCollection<Guid>>(), _companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Guid _, CancellationToken _) => _payments
                .Where(p => ids.Contains(p.Id) && p.TransferBankId.HasValue)
                .ToDictionary(p => p.Id, p => p.TransferBankId!.Value));

        var drawers = new Mock<ICashDrawerRepository>();
        return (
            new GetCashSessionSummaryHandler(user.Object, drawers.Object, sessions.Object, sales.Object, banks.Object, payments.Object),
            new GetCurrentCashSessionHandler(user.Object, drawers.Object, sessions.Object, sales.Object,
                new Mock<IPurchaseRepository>().Object, users.Object, payments.Object));
    }

    [Fact]
    public async Task Caja_ElDesglosePorBancoSumaLasTransferenciasCobradas_YRestaLasAnuladas()
    {
        AddCcSale("VENT-0101", 300_000m);
        (await Handler().Handle(
            new AddCustomerReceiptCommand(_customer.Id.Value, new DateTime(2026, 10, 2), null,
                [
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Transfer, 50_000m, null, TransferBankId: _bank.Id),
                    new AddCustomerReceiptLine((int)SalePaymentMethod.Cash, 10_000m, null),
                ]),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        var transfer = _payments.Single(p => p.Method == SalePaymentMethod.Transfer);
        var (summary, current) = CashHandlers();

        (await summary.Handle(new GetCashSessionSummaryQuery(_session.Id.Value), CancellationToken.None))
            .Value.TransferBankBreakdown.Select(b => (b.BankName, b.Amount))
            .Should().Equal(("Banco Nación", 50_000m));
        var movements = (await current.Handle(new GetCurrentCashSessionQuery(_session.CashDrawerId.Value), CancellationToken.None))
            .Value.Movements;
        movements.Single(m => m.CustomerPaymentId == transfer.Id).TransferBankId.Should().Be(_bank.Id);
        movements.Where(m => m.CustomerPaymentId != transfer.Id).Should().OnlyContain(m => m.TransferBankId == null);

        (await CancelReceiptHandler().Handle(
            new CancelCustomerReceiptCommand(_customer.Id.Value, _receipts.Single().Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        (await summary.Handle(new GetCashSessionSummaryQuery(_session.Id.Value), CancellationToken.None))
            .Value.TransferBankBreakdown.Should().BeEmpty("cobrada y anulada en la misma sesión: neto cero");
    }
}
