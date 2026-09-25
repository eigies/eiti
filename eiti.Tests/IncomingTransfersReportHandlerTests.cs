using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Reports.Queries.IncomingTransfersReport;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using System.Reflection;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class IncomingTransfersReportHandlerTests
{
    private const int MercadoPagoId = 3;
    private const int GaliciaId = 5;

    [Fact]
    public async Task Handle_ShouldListSaleTransfersAndCcCollections_WithReceivingBankAndNames()
    {
        var f = new Fixture();
        var sale = f.RetailSale(transferBankId: MercadoPagoId, amount: 95000m, code: "SUCU-001");
        var movement = f.TransferIncome(sale, 95000m);
        var collection = f.CcCollection(SalePaymentMethod.Transfer, 50000m, MercadoPagoId, reference: "REC-9");
        f.Sales(sale).Movements(movement).CcPayments(collection);

        var result = await f.Handler().Handle(Fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(2);
        var saleRow = result.Value.Rows.Single(r => r.Source == "sale");
        saleRow.Should().BeEquivalentTo(new
        {
            OccurredAt = movement.OccurredAt,
            Amount = 95000m,
            BankId = (int?)MercadoPagoId,
            BankName = "Mercadopago",
            Reference = "SUCU-001",
            CustomerName = f.Customer.FullName,
            BranchName = "Centro",
        });
        var ccRow = result.Value.Rows.Single(r => r.Source == "cc_collection");
        ccRow.Should().BeEquivalentTo(new
        {
            Amount = 50000m,
            BankId = (int?)MercadoPagoId,
            BankName = "Mercadopago",
            Reference = "REC-9",
            CustomerName = f.Customer.FullName,
            BranchName = "Centro",
        });
    }

    [Fact]
    public async Task Handle_ShouldIgnoreNonTransferPaymentsAndDirectPaymentsOnCcSales()
    {
        var f = new Fixture();
        var cash = f.RetailSale(transferBankId: null, amount: 1000m, code: "CASH-1", method: SalePaymentMethod.Cash);
        var ccSale = f.CcSaleWithDirectTransfer(2000m);
        var cashCollection = f.CcCollection(SalePaymentMethod.Cash, 3000m, null);
        f.Sales(cash, ccSale).CcPayments(cashCollection);

        var result = await f.Handler().Handle(Fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFilterByBank_ExcludingOtherBanksAndTransfersWithoutBank()
    {
        var f = new Fixture();
        var mp = f.RetailSale(transferBankId: MercadoPagoId, amount: 100m, code: "V-MP");
        var galicia = f.RetailSale(transferBankId: GaliciaId, amount: 200m, code: "V-GAL");
        var noBank = f.RetailSale(transferBankId: null, amount: 300m, code: "V-NOBANK");
        var ccNoBank = f.CcCollection(SalePaymentMethod.Transfer, 400m, null);
        f.Sales(mp, galicia, noBank).CcPayments(ccNoBank);

        var result = await f.Handler().Handle(Fixture.Query(bankId: MercadoPagoId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Select(r => r.Reference).Should().Equal("V-MP");
    }

    [Fact]
    public async Task Handle_ShouldFallBackToSaleTimestamps_WhenThereIsNoTransferIncomeMovement()
    {
        var f = new Fixture();
        var sale = f.RetailSale(transferBankId: MercadoPagoId, amount: 100m, code: "V-1");
        f.Sales(sale);

        var result = await f.Handler().Handle(Fixture.Query(), CancellationToken.None);

        result.Value.Rows.Single().OccurredAt.Should().Be(sale.PaidAt ?? sale.CreatedAt);
    }

    [Fact]
    public async Task Handle_ShouldPassAllowedBranchesToEveryQuery_WhenUserCannotSeeAllBranches()
    {
        var f = new Fixture(canViewAllBranches: false);
        f.Sales();

        await f.Handler().Handle(Fixture.Query(), CancellationToken.None);

        f.SaleRepository.Verify(r => r.ListWithPaymentsForReportAsync(
            f.CompanyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(f.AllowedBranches)), It.IsAny<CancellationToken>()));
        f.CustomerPaymentRepository.Verify(r => r.ListForPaymentMethodsReportAsync(
            f.CompanyId.Value, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(f.AllowedBranches)), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(62, true)]
    [InlineData(63, false)]
    public void Validator_ShouldLimitTheRangeTo62Days(int days, bool valid)
    {
        var from = new DateTime(2026, 7, 1);
        var result = new IncomingTransfersReportValidator().Validate(
            new IncomingTransfersReportQuery(from, from.AddDays(days)));

        result.IsValid.Should().Be(valid);
    }

    private sealed class Fixture
    {
        public CompanyId CompanyId { get; } = CompanyId.New();
        public Branch Branch { get; }
        public Customer Customer { get; }
        public UserId UserId { get; } = UserId.New();
        public IReadOnlyCollection<Guid> AllowedBranches { get; }
        public Mock<ISaleRepository> SaleRepository { get; } = new();
        public Mock<ICustomerPaymentRepository> CustomerPaymentRepository { get; } = new();
        private readonly Mock<ICurrentUserService> _currentUser = new();
        private readonly Mock<ICashSessionRepository> _cashSessions = new();
        private readonly Mock<IBankRepository> _banks = new();
        private readonly Mock<ICustomerRepository> _customers = new();
        private readonly Mock<IBranchRepository> _branches = new();
        private readonly Product _product;
        private readonly CashSession _session;

        public Fixture(bool canViewAllBranches = true)
        {
            Branch = Branch.Create(CompanyId, "Centro", "C", "San Martin 123");
            Customer = Customer.Create(CompanyId, "Juan", "Perez", null);
            AllowedBranches = [Branch.Id.Value];
            _product = Product.Create(CompanyId, "BAT-1", "BAT-1", "Moura", "Bateria", null, 1000m, 500m, null);
            _session = CashSession.Open(CompanyId, Branch.Id, CashDrawerId.New(), UserId, 0m, null);

            _currentUser.SetupGet(s => s.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(s => s.CompanyId).Returns(CompanyId);
            _currentUser.SetupGet(s => s.CanViewAllBranches).Returns(canViewAllBranches);
            _currentUser.SetupGet(s => s.AllowedBranchIds).Returns(AllowedBranches);

            var mercadoPago = Bank.Create(CompanyId, "Mercadopago", useForCard: false, useForTransfer: true, useForCheque: false);
            var galicia = Bank.Create(CompanyId, "BANCO GALICIA", useForCard: true, useForTransfer: true, useForCheque: true);
            SetId(mercadoPago, MercadoPagoId);
            SetId(galicia, GaliciaId);
            _banks.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([mercadoPago, galicia]);
            _customers.Setup(r => r.ListByIdsAsync(CompanyId, It.IsAny<IEnumerable<CustomerId>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([Customer]);
            _branches.Setup(r => r.ListByCompanyAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([Branch]);
            Movements();
            CcPayments();
        }

        public static IncomingTransfersReportQuery Query(int? bankId = null) =>
            new(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), bankId);

        public Sale RetailSale(int? transferBankId, decimal amount, string code, SalePaymentMethod method = SalePaymentMethod.Transfer)
        {
            var payment = SalePayment.Create(method, amount, null);
            payment.SetTransferBank(transferBankId);
            var sale = Sale.Create(CompanyId, Branch.Id, Customer.Id, false, SaleStatus.OnHold,
                [SaleDetail.Create(_product.Id, 1, amount)], [payment], allowOverpayment: true, code: code);
            sale.MarkAsPaid(_session.CashDrawerId, _session.Id);
            return sale;
        }

        public Sale CcSaleWithDirectTransfer(decimal amount)
        {
            var sale = Sale.CreateCc(CompanyId, Branch.Id, Customer.Id, [SaleDetail.Create(_product.Id, 1, amount)], code: "CC-1");
            // Anomalía conocida (ver lessons.md): un SalePayment directo sobre una venta CC.
            var payment = SalePayment.Create(SalePaymentMethod.Transfer, amount, null);
            payment.SetTransferBank(MercadoPagoId);
            var payments = (List<SalePayment>)typeof(Sale)
                .GetField("_payments", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(sale)!;
            payments.Add(payment);
            return sale;
        }

        public CashMovement TransferIncome(Sale sale, decimal amount) =>
            CashMovement.Create(_session.Id, CashMovementType.TransferIncome, CashMovementDirection.In, amount,
                "Sale", sale.Id.Value, "Pago por transferencia", UserId);

        public CustomerPayment CcCollection(SalePaymentMethod method, decimal amount, int? bankId, string? reference = null)
        {
            var payment = CustomerPayment.Create(CompanyId.Value, Customer.Id.Value, Branch.Id.Value, method, amount,
                new DateTime(2026, 9, 10), reference, null, UserId.Value);
            payment.SetTransferBank(bankId);
            return payment;
        }

        public Fixture Sales(params Sale[] sales)
        {
            SaleRepository.Setup(r => r.ListWithPaymentsForReportAsync(CompanyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                    It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sales);
            return this;
        }

        public Fixture Movements(params CashMovement[] movements)
        {
            _cashSessions.Setup(r => r.ListMovementsByCompanyAsync(CompanyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                    It.IsAny<IReadOnlyCollection<CashMovementType>>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(movements);
            return this;
        }

        public Fixture CcPayments(params CustomerPayment[] payments)
        {
            CustomerPaymentRepository.Setup(r => r.ListForPaymentMethodsReportAsync(CompanyId.Value, It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(payments);
            return this;
        }

        public IncomingTransfersReportHandler Handler() => new(
            _currentUser.Object, SaleRepository.Object, CustomerPaymentRepository.Object, _cashSessions.Object,
            _banks.Object, _customers.Object, _branches.Object);

        private static void SetId(Bank bank, int id) =>
            typeof(Bank).GetProperty(nameof(Bank.Id))!.SetValue(bank, id);
    }
}
