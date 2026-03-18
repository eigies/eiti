using System.Globalization;
using System.Linq;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;

public sealed class SendSaleWhatsAppHandler
    : IRequestHandler<SendSaleWhatsAppCommand, Result<SendSaleWhatsAppResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;

    public SendSaleWhatsAppHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _companyRepository = companyRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<SendSaleWhatsAppResponse>> Handle(
        SendSaleWhatsAppCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SendSaleWhatsAppResponse>.Failure(authCheck.Error);
        var companyId = _currentUserService.CompanyId!;

        if (!_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.Forbidden);
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.Id), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.NotFound);
        }

        if (sale.SaleStatus != SaleStatus.Paid)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.NotPaid);
        }

        if (sale.CustomerId is null)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.CustomerRequired);
        }

        var customer = await _customerRepository.GetByIdAsync(
            new CustomerId(sale.CustomerId.Value),
            companyId,
            cancellationToken);

        if (customer is null)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.CustomerNotFound);
        }

        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.CustomerPhoneRequired);
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.CompanyNotFound);
        }

        if (!company.IsWhatsAppEnabled)
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.Disabled);
        }

        if (string.IsNullOrWhiteSpace(company.WhatsAppSenderPhone))
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.SenderPhoneRequired);
        }

        var toPhoneDigits = NormalizePhoneForWaMe(customer.Phone);
        if (string.IsNullOrWhiteSpace(toPhoneDigits))
        {
            return Result<SendSaleWhatsAppResponse>.Failure(SendSaleWhatsAppErrors.CustomerPhoneInvalid);
        }

        var branch = await _branchRepository.GetByIdAsync(sale.BranchId, companyId, cancellationToken);
        var branchName = branch?.Name;

        var productLines = await BuildProductLinesAsync(sale, companyId, cancellationToken);
        var paymentSummary = BuildPaymentSummary(sale);
        var message = BuildMessage(
            customer.FullName,
            sale.TotalAmount,
            paymentSummary,
            branchName,
            company.Name.Value,
            productLines);
        var launchUrl = BuildWaMeUrl(toPhoneDigits, message);

        return Result<SendSaleWhatsAppResponse>.Success(
            new SendSaleWhatsAppResponse(
                sale.Id.Value,
                customer.Phone,
                message,
                launchUrl,
                true));
    }

    private async Task<IReadOnlyList<string>> BuildProductLinesAsync(
        Sale sale,
        Domain.Companies.CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var productLines = new List<string>();

        foreach (var detail in sale.Details)
        {
            var product = await _productRepository.GetByIdAsync(detail.ProductId, companyId, cancellationToken);
            var productName = product is null
                ? "Producto"
                : string.IsNullOrWhiteSpace(product.Brand)
                    ? product.Name
                    : $"{product.Brand} {product.Name}";

            productLines.Add($"- {detail.Quantity} x {productName}");
        }

        return productLines;
    }

    private static string BuildMessage(
        string customerName,
        decimal totalAmount,
        string paymentSummary,
        string? branchName,
        string companyName,
        IReadOnlyList<string> productLines)
    {
        var safeName = string.IsNullOrWhiteSpace(customerName) ? "cliente" : customerName.Trim();
        var amountText = FormatCurrency(totalAmount);
        var safeBranch = string.IsNullOrWhiteSpace(branchName) ? "Sucursal no informada" : branchName.Trim();
        var safeCompany = string.IsNullOrWhiteSpace(companyName) ? "nuestra empresa" : companyName.Trim();
        var productsText = productLines.Count > 0
            ? string.Join(Environment.NewLine, productLines)
            : "- Producto no disponible";

        return string.Join(
            Environment.NewLine,
            $"Hola {safeName}!",
            string.Empty,
            "Gracias por tu compra. Te compartimos el resumen de tu pedido:",
            $"*Valor total:* {amountText}",
            $"*Metodo de pago:* {paymentSummary}",
            $"*Sucursal:* {safeBranch}",
            $"*Compania:* {safeCompany}",
            "*Producto adquirido:*",
            productsText,
            string.Empty,
            "Cualquier duda, estamos para ayudarte. Gracias por elegirnos.");
    }

    private static string BuildPaymentSummary(Sale sale)
    {
        if (sale.Payments.Count == 0)
        {
            return "No informado";
        }

        return string.Join(
            ", ",
            sale.Payments.Select(payment =>
            {
                var label = payment.Method switch
                {
                    SalePaymentMethod.Cash => "Efectivo",
                    SalePaymentMethod.Transfer => "Transferencia",
                    SalePaymentMethod.Card => "Tarjeta",
                    SalePaymentMethod.Check => "Cheque",
                    SalePaymentMethod.Other => "Otro",
                    _ => "Pago"
                };

                return $"{label} ({FormatCurrency(payment.Amount)})";
            }));
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("$ #,##0.00", CultureInfo.GetCultureInfo("es-AR"));
    }

    private static string BuildWaMeUrl(string phoneDigits, string message)
    {
        return $"https://wa.me/{phoneDigits}?text={Uri.EscapeDataString(message)}";
    }

    private static string NormalizePhoneForWaMe(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
