using System.Reflection;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Behaviors;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Services;

public sealed class AuditSnapshotService : IAuditSnapshotService
{
    private static readonly HashSet<string> SaleCreateActions =
    [
        "CreateSaleCommand",
        "CreateCcSaleCommand"
    ];

    private static readonly HashSet<string> SaleActions =
    [
        "UpdateSaleCommand",
        "DeleteSaleCommand",
        "CancelSaleCommand",
        "SendSaleWhatsAppCommand",
        "AddCcPaymentCommand",
        "AddCcPaymentGroupCommand",
        "CancelCcPaymentCommand"
    ];

    private static readonly HashSet<string> PurchaseCreateActions =
    [
        "CreatePurchaseCommand"
    ];

    private static readonly HashSet<string> PurchaseActions =
    [
        "AddPurchasePaymentCommand",
        "CancelPurchaseCommand",
        "CancelPurchasePaymentCommand"
    ];

    private static readonly HashSet<string> ProductCreateActions =
    [
        "CreateProductCommand"
    ];

    private static readonly HashSet<string> ProductActions =
    [
        "UpdateProductCommand",
        "DeleteProductCommand"
    ];

    private static readonly HashSet<string> CustomerCreateActions =
    [
        "CreateCustomerCommand"
    ];

    private static readonly HashSet<string> CustomerActions =
    [
        "UpdateCustomerCommand"
    ];

    // Cobros de cuenta corriente a nivel cliente (la "bolsa"): se audita el CustomerPayment con su
    // desglose de imputaciones + el saldo a favor del cliente. AddCustomerPayment crea el cobro (snapshot
    // solo "después", con el PaymentId de la respuesta); CancelCustomerPayment lo muta (snapshot antes/después).
    private static readonly HashSet<string> CustomerPaymentCreateActions =
    [
        "AddCustomerPaymentCommand"
    ];

    private static readonly HashSet<string> CustomerPaymentActions =
    [
        "CancelCustomerPaymentCommand"
    ];

    private static readonly HashSet<string> SupplierCreateActions =
    [
        "CreateSupplierCommand"
    ];

    private static readonly HashSet<string> SupplierActions =
    [
        "UpdateSupplierCommand",
        "DeactivateSupplierCommand"
    ];

    private readonly ApplicationDbContext _context;

    public AuditSnapshotService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> CaptureBeforeAsync(
        object request,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var actionType = request.GetType().Name;

        if (SaleActions.Contains(actionType) && TryGetSaleId(request, out var saleId))
            return Serialize(await BuildSaleSnapshotAsync(saleId, companyId, cancellationToken));

        if (PurchaseActions.Contains(actionType) && TryGetPurchaseId(request, out var purchaseId))
            return Serialize(await BuildPurchaseSnapshotAsync(purchaseId, companyId, cancellationToken));

        if (ProductActions.Contains(actionType) && TryGetGuid(request, "Id", out var productId))
            return Serialize(await BuildProductSnapshotAsync(productId, companyId, cancellationToken));

        if (CustomerActions.Contains(actionType) && TryGetGuid(request, "Id", out var customerId))
            return Serialize(await BuildCustomerSnapshotAsync(customerId, companyId, cancellationToken));

        if (CustomerPaymentActions.Contains(actionType) && TryGetGuid(request, "PaymentId", out var cancelPaymentId))
            return Serialize(await BuildCustomerPaymentSnapshotAsync(cancelPaymentId, companyId, cancellationToken));

        if (SupplierActions.Contains(actionType) && TryGetGuid(request, "Id", out var supplierId))
            return Serialize(await BuildSupplierSnapshotAsync(supplierId, companyId, cancellationToken));

        if (actionType == "AdjustStockCommand"
            && TryGetGuid(request, "BranchId", out var branchId)
            && TryGetGuid(request, "ProductId", out var stockProductId))
        {
            return Serialize(await BuildStockSnapshotAsync(branchId, stockProductId, companyId, cancellationToken));
        }

        return null;
    }

    public async Task<string?> CaptureAfterAsync(
        object request,
        object? response,
        bool succeeded,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (!succeeded)
            return null;

        var actionType = request.GetType().Name;

        if ((SaleCreateActions.Contains(actionType) && TryGetResponseId(response, out var createdSaleId))
            || (SaleActions.Contains(actionType) && TryGetSaleId(request, out createdSaleId)))
        {
            return Serialize(await BuildSaleSnapshotAsync(createdSaleId, companyId, cancellationToken));
        }

        if ((PurchaseCreateActions.Contains(actionType) && TryGetResponseId(response, out var createdPurchaseId))
            || (PurchaseActions.Contains(actionType) && TryGetPurchaseId(request, out createdPurchaseId)))
        {
            return Serialize(await BuildPurchaseSnapshotAsync(createdPurchaseId, companyId, cancellationToken));
        }

        if ((ProductCreateActions.Contains(actionType) && TryGetResponseId(response, out var createdProductId))
            || (ProductActions.Contains(actionType) && TryGetGuid(request, "Id", out createdProductId)))
        {
            return Serialize(await BuildProductSnapshotAsync(createdProductId, companyId, cancellationToken));
        }

        if ((CustomerCreateActions.Contains(actionType) && TryGetResponseId(response, out var createdCustomerId))
            || (CustomerActions.Contains(actionType) && TryGetGuid(request, "Id", out createdCustomerId)))
        {
            return Serialize(await BuildCustomerSnapshotAsync(createdCustomerId, companyId, cancellationToken));
        }

        if ((CustomerPaymentCreateActions.Contains(actionType) && TryGetResponseGuid(response, "PaymentId", out var customerPaymentId))
            || (CustomerPaymentActions.Contains(actionType) && TryGetGuid(request, "PaymentId", out customerPaymentId)))
        {
            return Serialize(await BuildCustomerPaymentSnapshotAsync(customerPaymentId, companyId, cancellationToken));
        }

        if ((SupplierCreateActions.Contains(actionType) && TryGetResponseId(response, out var createdSupplierId))
            || (SupplierActions.Contains(actionType) && TryGetGuid(request, "Id", out createdSupplierId)))
        {
            return Serialize(await BuildSupplierSnapshotAsync(createdSupplierId, companyId, cancellationToken));
        }

        if (actionType == "AdjustStockCommand"
            && TryGetGuid(request, "BranchId", out var branchId)
            && TryGetGuid(request, "ProductId", out var stockProductId))
        {
            return Serialize(await BuildStockSnapshotAsync(branchId, stockProductId, companyId, cancellationToken));
        }

        return null;
    }

    private async Task<object?> BuildSaleSnapshotAsync(Guid saleId, Guid companyId, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .Include(s => s.TradeIns)
            .Include(s => s.CcPayments)
            .FirstOrDefaultAsync(
                s => s.Id == new SaleId(saleId) && s.CompanyId == new CompanyId(companyId),
                cancellationToken);

        if (sale is null)
            return null;

        return new
        {
            Entity = "Sale",
            Id = sale.Id.Value,
            sale.Code,
            BranchId = sale.BranchId.Value,
            CustomerId = sale.CustomerId?.Value,
            CashDrawerId = sale.CashDrawerId?.Value,
            Status = (int)sale.SaleStatus,
            StatusName = sale.SaleStatus.ToString(),
            sale.HasDelivery,
            sale.DeliveryAddress,
            sale.IsCuentaCorriente,
            sale.SourceChannel,
            sale.NoDeliverySurchargeTotal,
            sale.CardSurchargeTotal,
            sale.GeneralDiscountPercent,
            sale.OriginalTotal,
            sale.TotalAmount,
            sale.EffectiveTotal,
            sale.SettledAmount,
            sale.PendingAmount,
            sale.ChangeAmount,
            sale.CreatedAt,
            sale.PaidAt,
            sale.UpdatedAt,
            Details = sale.Details
                .OrderBy(d => d.ProductId.Value)
                .Select(d => new
                {
                    ProductId = d.ProductId.Value,
                    d.Quantity,
                    d.UnitPrice,
                    d.DiscountPercent,
                    d.TotalAmount
                })
                .ToList(),
            Payments = sale.Payments
                .OrderBy(p => (int)p.Method)
                .Select(p => new
                {
                    Method = (int)p.Method,
                    MethodName = p.Method.ToString(),
                    p.Amount,
                    p.Reference,
                    p.CardBankId,
                    p.CardCuotas,
                    p.CardSurchargePct,
                    p.CardSurchargeAmt,
                    p.TotalCobrado,
                    p.TransferBankId
                })
                .ToList(),
            TradeIns = sale.TradeIns
                .OrderBy(t => t.ProductId.Value)
                .Select(t => new
                {
                    ProductId = t.ProductId.Value,
                    t.Quantity,
                    t.Amount
                })
                .ToList(),
            CcPayments = sale.CcPayments
                .OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    Id = p.Id.Value,
                    Method = (int)p.Method,
                    MethodName = p.Method.ToString(),
                    p.Amount,
                    Status = (int)p.Status,
                    StatusName = p.Status.ToString(),
                    p.Date,
                    p.Notes,
                    p.GroupId,
                    p.CancelledAt,
                    p.CardBankId,
                    p.CardCuotas,
                    p.CardSurchargePct,
                    p.CardSurchargeAmt,
                    p.TotalCobrado
                })
                .ToList()
        };
    }

    private async Task<object?> BuildPurchaseSnapshotAsync(Guid purchaseId, Guid companyId, CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Details)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(
                p => p.Id == purchaseId && p.CompanyId == companyId,
                cancellationToken);

        if (purchase is null)
            return null;

        return new
        {
            Entity = "Purchase",
            purchase.Id,
            purchase.Code,
            purchase.BranchId,
            purchase.SupplierId,
            Status = (int)purchase.Status,
            StatusName = purchase.Status.ToString(),
            purchase.InvoiceNumber,
            purchase.Notes,
            purchase.IvaPct,
            purchase.IngresosBrutosPct,
            purchase.TotalAmount,
            purchase.TaxAmount,
            purchase.GrandTotal,
            purchase.TotalPaid,
            purchase.PendingAmount,
            purchase.CreatedAt,
            purchase.PaidAt,
            purchase.CancelledAt,
            Details = purchase.Details
                .OrderBy(d => d.ProductName)
                .Select(d => new
                {
                    d.Id,
                    d.ProductId,
                    d.ProductName,
                    d.Quantity,
                    d.UnitCost,
                    d.TotalAmount
                })
                .ToList(),
            Payments = purchase.Payments
                .OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    Method = (int)p.Method,
                    MethodName = p.Method.ToString(),
                    p.Amount,
                    Status = (int)p.Status,
                    StatusName = p.Status.ToString(),
                    p.Reference,
                    p.Notes,
                    p.Date,
                    p.CreatedAt
                })
                .ToList()
        };
    }

    private async Task<object?> BuildProductSnapshotAsync(Guid productId, Guid companyId, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == new ProductId(productId) && p.CompanyId == new CompanyId(companyId),
                cancellationToken);

        if (product is null)
            return null;

        return new
        {
            Entity = "Product",
            Id = product.Id.Value,
            product.Code,
            product.Sku,
            product.Brand,
            product.Name,
            product.Description,
            PublicPrice = product.Price,
            product.CostPrice,
            product.UnitPrice,
            product.AllowsManualValueInSale,
            product.NoDeliverySurcharge,
            product.CreatedAt,
            product.UpdatedAt
        };
    }

    private async Task<object?> BuildCustomerSnapshotAsync(Guid customerId, Guid companyId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == new CustomerId(customerId) && c.CompanyId == new CompanyId(companyId),
                cancellationToken);

        if (customer is null)
            return null;

        return new
        {
            Entity = "Customer",
            Id = customer.Id.Value,
            customer.Name,
            customer.FirstName,
            customer.LastName,
            customer.FullName,
            Email = customer.Email == null ? null : customer.Email.Value,
            customer.Phone,
            DocumentType = customer.DocumentType.HasValue ? (int)customer.DocumentType.Value : (int?)null,
            DocumentTypeName = customer.DocumentType?.ToString(),
            customer.DocumentNumber,
            customer.TaxId,
            AddressId = customer.AddressId?.Value,
            customer.CreditBalance,
            customer.CreatedAt,
            customer.UpdatedAt
        };
    }

    private async Task<object?> BuildCustomerPaymentSnapshotAsync(Guid paymentId, Guid companyId, CancellationToken cancellationToken)
    {
        var payment = await _context.CustomerPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.CompanyId == companyId, cancellationToken);

        if (payment is null)
            return null;

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == new CustomerId(payment.CustomerId) && c.CompanyId == new CompanyId(companyId),
                cancellationToken);

        // Imputaciones: filas SaleCcPayment (método CustomerCredit) generadas por este cobro.
        var imputaciones = await _context.SaleCcPayments
            .AsNoTracking()
            .Where(p => p.CustomerPaymentId == paymentId)
            .Select(p => new
            {
                SaleId = p.SaleId.Value,
                Method = (int)p.Method,
                p.Amount,
                Status = (int)p.Status
            })
            .ToListAsync(cancellationToken);

        return new
        {
            Entity = "CustomerPayment",
            Id = payment.Id,
            payment.CustomerId,
            CustomerName = customer?.Name,
            Method = (int)payment.Method,
            MethodName = payment.Method.ToString(),
            payment.Amount,
            Status = (int)payment.Status,
            StatusName = payment.Status.ToString(),
            payment.Date,
            payment.Reference,
            payment.Notes,
            payment.ChequeId,
            payment.CardBankId,
            payment.CardCuotas,
            payment.CardSurchargePct,
            payment.CardSurchargeAmt,
            payment.TotalCobrado,
            CustomerCreditBalance = customer?.CreditBalance,
            Imputaciones = imputaciones
        };
    }

    private async Task<object?> BuildSupplierSnapshotAsync(Guid supplierId, Guid companyId, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == supplierId && s.CompanyId == companyId,
                cancellationToken);

        if (supplier is null)
            return null;

        return new
        {
            Entity = "Supplier",
            supplier.Id,
            supplier.Name,
            supplier.Phone,
            supplier.Email,
            supplier.TaxId,
            supplier.Notes,
            supplier.IsActive,
            supplier.CreditBalance,
            supplier.CreatedAt
        };
    }

    private async Task<object?> BuildStockSnapshotAsync(
        Guid branchId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var stock = await _context.BranchProductStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.BranchId == new BranchId(branchId)
                    && s.ProductId == new ProductId(productId)
                    && s.CompanyId == new CompanyId(companyId),
                cancellationToken);

        return new
        {
            Entity = "BranchProductStock",
            BranchId = branchId,
            ProductId = productId,
            OnHandQuantity = stock?.OnHandQuantity ?? 0,
            ReservedQuantity = stock?.ReservedQuantity ?? 0,
            AvailableQuantity = stock?.AvailableQuantity ?? 0,
            UpdatedAt = stock?.UpdatedAt
        };
    }

    private static string? Serialize(object? snapshot)
    {
        return snapshot is null
            ? null
            : AuditPayloadSerializer.Serialize(snapshot);
    }

    private static bool TryGetSaleId(object request, out Guid saleId)
    {
        return TryGetGuid(request, "SaleId", out saleId)
            || TryGetGuid(request, "Id", out saleId);
    }

    private static bool TryGetPurchaseId(object request, out Guid purchaseId)
    {
        return TryGetGuid(request, "PurchaseId", out purchaseId)
            || TryGetGuid(request, "Id", out purchaseId);
    }

    private static bool TryGetGuid(object source, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(source) is Guid guid)
        {
            value = guid;
            return true;
        }

        return false;
    }

    private static bool TryGetResponseId(object? response, out Guid id)
    {
        id = Guid.Empty;
        if (response is null)
            return false;

        var responseValue = response.GetType().GetProperty("Value")?.GetValue(response);
        var source = responseValue ?? response;

        return TryGetGuid(source, "Id", out id);
    }

    private static bool TryGetResponseGuid(object? response, string propertyName, out Guid id)
    {
        id = Guid.Empty;
        if (response is null)
            return false;

        var responseValue = response.GetType().GetProperty("Value")?.GetValue(response);
        var source = responseValue ?? response;

        return TryGetGuid(source, propertyName, out id);
    }
}
