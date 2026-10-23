using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Customers.Commands.AddCustomerReceipt;
using eiti.Application.Features.Customers.Common;
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

public sealed class AddCustomerReceiptHandlerTests
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
    private readonly Mock<IStockMovementRepository> _stockMovements = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly BranchProductStock _stock;
    private readonly ProductId _productId = ProductId.New();
    private Bank _bank;

    public AddCustomerReceiptHandlerTests()
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

        var payments = new Mock<ICustomerPaymentRepository>();
        payments
            .Setup(r => r.AddAsync(It.IsAny<CustomerPayment>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerPayment, CancellationToken>((p, _) => _payments.Add(p))
            .Returns(Task.CompletedTask);

        var receipts = new Mock<ICustomerReceiptRepository>();
        receipts
            .Setup(r => r.CountByCompanyAsync(_companyId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReceipts);
        receipts
            .Setup(r => r.AddAsync(It.IsAny<CustomerReceipt>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerReceipt, CancellationToken>((r, _) => _receipts.Add(r))
            .Returns(Task.CompletedTask);

        var sales = new Mock<ISaleRepository>();
        sales
            .Setup(r => r.ListPendingCcSalesByCustomerAsync(_companyId, _customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _pending.ToList());

        var banks = new Mock<IBankRepository>();
        banks
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _bank);

        var cheques = new Mock<IChequeRepository>();
        cheques
            .Setup(r => r.AddAsync(It.IsAny<Cheque>(), It.IsAny<CancellationToken>()))
            .Callback<Cheque, CancellationToken>((c, _) => _cheques.Add(c))
            .Returns(Task.CompletedTask);

        var stocks = new Mock<IBranchProductStockRepository>();
        stocks
            .Setup(r => r.GetOrCreateAsync(_branch.Id, _productId, _companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_stock);

        var service = new CustomerReceiptService(
            customers.Object,
            payments.Object,
            receipts.Object,
            sales.Object,
            banks.Object,
            cheques.Object,
            stocks.Object,
            _stockMovements.Object);

        return new AddCustomerReceiptHandler(
            currentUser.Object, customers.Object, drawers.Object, sessions.Object, service, _unitOfWork.Object);
    }
}
