using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Features.Customers.Commands.AddCustomerPayment;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class AddCustomerPaymentHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRejectCardPayment_WhenBankIsNotEnabledForCard()
    {
        var companyId = CompanyId.New();
        var bank = Bank.Create(companyId, "Banco tarjeta", useForCard: false, useForTransfer: true, useForCheque: true);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdAsync(bank.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);
        var (handler, customer) = CreateHandler(companyId, bankRepository.Object);

        var result = await handler.Handle(
            new AddCustomerPaymentCommand(
                customer.Id.Value,
                (int)SalePaymentMethod.Card,
                100m,
                DateTime.UtcNow.Date,
                null,
                null,
                CardBankId: bank.Id,
                CardCuotas: 1),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.AddPayment.CardBankInvalid");
    }

    [Fact]
    public async Task Handle_ShouldRejectChequePayment_WhenBankIsNotEnabledForCheque()
    {
        var companyId = CompanyId.New();
        var bank = Bank.Create(companyId, "Banco cheque", useForCard: true, useForTransfer: true, useForCheque: false);
        var bankRepository = new Mock<IBankRepository>();
        bankRepository
            .Setup(repository => repository.GetByIdAsync(bank.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);
        var (handler, customer) = CreateHandler(companyId, bankRepository.Object);

        var result = await handler.Handle(
            new AddCustomerPaymentCommand(
                customer.Id.Value,
                (int)SalePaymentMethod.Check,
                100m,
                DateTime.UtcNow.Date,
                null,
                null,
                Cheque: new AddCustomerPaymentChequeData(
                    bank.Id,
                    "000123",
                    "Juan Perez",
                    "20123456789",
                    100m,
                    DateTime.UtcNow.Date,
                    DateTime.UtcNow.Date.AddDays(30),
                    null)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Customers.AddPayment.ChequeBankInvalid");
    }

    private static (AddCustomerPaymentHandler Handler, Customer Customer) CreateHandler(
        CompanyId companyId,
        IBankRepository bankRepository)
    {
        var userId = UserId.New();
        var branch = Branch.Create(companyId, "Sucursal Centro", "SC", "San Martin 123");
        var drawer = CashDrawer.Create(companyId, branch.Id, "Caja 1");
        var session = CashSession.Open(companyId, branch.Id, drawer.Id, userId, 0m, null);
        var customer = Customer.Create(companyId, "Juan", "Perez", null);

        var currentUserService = new Mock<ICurrentUserService>();
        var customerRepository = new Mock<ICustomerRepository>();
        var customerPaymentRepository = new Mock<ICustomerPaymentRepository>();
        var saleRepository = new Mock<ISaleRepository>();
        var branchProductStockRepository = new Mock<IBranchProductStockRepository>();
        var stockMovementRepository = new Mock<IStockMovementRepository>();
        var cashDrawerRepository = new Mock<ICashDrawerRepository>();
        var cashSessionRepository = new Mock<ICashSessionRepository>();
        var chequeRepository = new Mock<IChequeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUserService.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(service => service.CompanyId).Returns(companyId);
        currentUserService.SetupGet(service => service.UserId).Returns(userId);

        customerRepository
            .Setup(repository => repository.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        cashDrawerRepository
            .Setup(repository => repository.GetByAssignedUserAsync(userId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawer);

        cashSessionRepository
            .Setup(repository => repository.GetOpenByDrawerAsync(drawer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        saleRepository
            .Setup(repository => repository.ListPendingCcSalesByCustomerAsync(companyId, customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<eiti.Domain.Sales.Sale>());

        customerPaymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<CustomerPayment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        chequeRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Cheque>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new AddCustomerPaymentHandler(
            currentUserService.Object,
            customerRepository.Object,
            customerPaymentRepository.Object,
            saleRepository.Object,
            branchProductStockRepository.Object,
            stockMovementRepository.Object,
            cashDrawerRepository.Object,
            cashSessionRepository.Object,
            bankRepository,
            chequeRepository.Object,
            unitOfWork.Object), customer);
    }
}
