using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using FluentAssertions;
using Moq;

namespace eiti.Tests;

public sealed class SendSaleWhatsAppHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnWaMeLaunchUrl_WhenSaleIsPaidAndConfigured()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();

        var customer = Customer.Create(companyId, "Juan", "Perez", Email.Create("juan@test.com"), "+54 9 11 2233-4455");
        var product = Product.Create(companyId, "BAT-001", "BAT-001", "Contoso", "Bateria", null, 100m, 70m, null);
        var sale = Sale.Create(
            companyId,
            branchId,
            customer.Id,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)],
            [SalePayment.Create(SalePaymentMethod.Transfer, 100m, null)],
            []);
        sale.MarkAsPaid(null);

        var company = Company.Create(CompanyName.Create("Mi Empresa"), CompanyDomain.Create("miempresa.local"));
        company.Update(CompanyName.Create("Mi Empresa"), CompanyDomain.Create("miempresa.local"), true, "+5491199988877");

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var companyRepository = new Mock<ICompanyRepository>();
        var branchRepository = new Mock<IBranchRepository>();
        var productRepository = new Mock<IProductRepository>();
        var branch = Branch.Create(companyId, "Casa Central", "CC", "Siempre Viva 123");

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        saleRepository.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        customerRepository.Setup(x => x.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        companyRepository.Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        branchRepository.Setup(x => x.GetByIdAsync(branchId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);
        productRepository.Setup(x => x.GetByIdAsync(product.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = new SendSaleWhatsAppHandler(
            currentUserService.Object,
            saleRepository.Object,
            customerRepository.Object,
            companyRepository.Object,
            branchRepository.Object,
            productRepository.Object);

        var result = await handler.Handle(new SendSaleWhatsAppCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SaleId.Should().Be(sale.Id.Value);
        result.Value.RequiresUserAction.Should().BeTrue();
        result.Value.LaunchUrl.Should().StartWith("https://wa.me/");
        result.Value.LaunchUrl.Should().Contain("text=");
        result.Value.ToPhone.Should().Be(customer.Phone);
        result.Value.Message.Should().Contain("*Valor total:*");
        result.Value.Message.Should().Contain("*Metodo de pago:* Transferencia");
        result.Value.Message.Should().Contain("*Sucursal:* Casa Central");
        result.Value.Message.Should().Contain("*Compania:* Mi Empresa");
        result.Value.Message.Should().Contain("*Producto adquirido:*");
        result.Value.Message.Should().Contain("- 1 x Contoso Bateria");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCompanyWhatsAppIsDisabled()
    {
        var companyId = CompanyId.New();
        var branchId = BranchId.New();

        var customer = Customer.Create(companyId, "Ana", "Lopez", Email.Create("ana@test.com"), "+5491166677788");
        var product = Product.Create(companyId, "BAT-002", "BAT-002", "Contoso", "Bateria", null, 100m, 70m, null);
        var sale = Sale.Create(
            companyId,
            branchId,
            customer.Id,
            false,
            SaleStatus.OnHold,
            [SaleDetail.Create(product.Id, 1, product.Price)],
            [SalePayment.Create(SalePaymentMethod.Transfer, 100m, null)],
            []);
        sale.MarkAsPaid(null);

        var company = Company.Create(CompanyName.Create("Mi Empresa"), CompanyDomain.Create("miempresa.local"));

        var currentUserService = new Mock<ICurrentUserService>();
        var saleRepository = new Mock<ISaleRepository>();
        var customerRepository = new Mock<ICustomerRepository>();
        var companyRepository = new Mock<ICompanyRepository>();
        var branchRepository = new Mock<IBranchRepository>();
        var productRepository = new Mock<IProductRepository>();

        currentUserService.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserService.SetupGet(x => x.CompanyId).Returns(companyId);
        currentUserService.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        saleRepository.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        customerRepository.Setup(x => x.GetByIdAsync(customer.Id, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        companyRepository.Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new SendSaleWhatsAppHandler(
            currentUserService.Object,
            saleRepository.Object,
            customerRepository.Object,
            companyRepository.Object,
            branchRepository.Object,
            productRepository.Object);

        var result = await handler.Handle(new SendSaleWhatsAppCommand(sale.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(
            Error.Conflict("Sales.SendWhatsApp.Disabled", "WhatsApp notifications are disabled for the current company."));
    }
}
