using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;

public sealed class GetSaleInvoicePrintHandler
    : IRequestHandler<GetSaleInvoicePrintQuery, Result<SaleInvoicePrintResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly IProductRepository _productRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IFiscalizationService _fiscalizationService;
    private readonly IBankRepository _bankRepository;

    public GetSaleInvoicePrintHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ISaleFiscalDocumentRepository fiscalDocuments,
        IProductRepository productRepository,
        IBranchRepository branchRepository,
        IFiscalizationService fiscalizationService,
        IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _fiscalDocuments = fiscalDocuments;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _fiscalizationService = fiscalizationService;
        _bankRepository = bankRepository;
    }

    public async Task<Result<SaleInvoicePrintResponse>> Handle(
        GetSaleInvoicePrintQuery query,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<SaleInvoicePrintResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        // El repositorio de ventas no filtra por empresa: el aislamiento se chequea acá.
        var sale = await _saleRepository.GetByIdAsync(new SaleId(query.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure<SaleInvoicePrintResponse>(GetSaleInvoicePrintErrors.NotFound);
        }

        var documents = await _fiscalDocuments.ListBySaleAsync(sale.Id, companyId, cancellationToken);
        var document = query.Kind == SaleFiscalDocumentKind.Invoice
            // Vigente o anulada: las dos tuvieron CAE y se tienen que poder reimprimir.
            ? SaleInvoicingView.From(documents).PrintableInvoice
            // NC/ND: la vigente; si no hay, la última que tuvo CAE (una NC neutralizada por su ND también existió).
            : documents
                .Where(candidate => candidate.Kind == query.Kind
                    && candidate.Status is SaleInvoicingStatus.Invoiced or SaleInvoicingStatus.Voided)
                .OrderByDescending(candidate => candidate.Status == SaleInvoicingStatus.Invoiced)
                .ThenByDescending(candidate => candidate.Sequence)
                .FirstOrDefault();

        if (document?.FiscalDocumentId is null)
        {
            return Result.Failure<SaleInvoicePrintResponse>(query.Kind switch
            {
                SaleFiscalDocumentKind.Invoice => GetSaleInvoicePrintErrors.NotInvoiced,
                SaleFiscalDocumentKind.DebitNote => GetSaleInvoicePrintErrors.NoDebitNote,
                _ => GetSaleInvoicePrintErrors.NoCreditNote
            });
        }

        var fiscal = await _fiscalizationService.GetPrintableDocumentAsync(
            companyId.Value, document.FiscalDocumentId.Value, cancellationToken);
        if (!fiscal.IsSuccess || fiscal.Document is null)
        {
            return Result.Failure<SaleInvoicePrintResponse>(GetSaleInvoicePrintErrors.Unavailable(fiscal.ErrorMessage));
        }

        var printed = fiscal.Document;
        var type = FiscalPrintLabels.DescribeType(printed.Type);
        if (type is null)
        {
            return Result.Failure<SaleInvoicePrintResponse>(
                GetSaleInvoicePrintErrors.Unavailable($"Tipo de comprobante desconocido: {printed.Type}."));
        }

        var products = (await _productRepository.GetByIdsAsync(
                sale.Details.Select(detail => detail.ProductId).Distinct(), companyId, cancellationToken))
            .ToDictionary(product => product.Id);
        var branch = await _branchRepository.GetByIdAsync(sale.BranchId, companyId, cancellationToken);

        var items = sale.Details
            .Select(detail => new SaleInvoicePrintItem(
                products.TryGetValue(detail.ProductId, out var product)
                    ? $"{product.Brand} / {product.Name}"
                    : "Producto",
                detail.Quantity,
                detail.UnitPrice,
                detail.DiscountPercent,
                detail.TotalAmount))
            .ToList();

        var vat = printed.Amounts.Vat
            .Select(line => new SaleInvoicePrintVat(line.Rate, line.Base, line.Amount))
            .ToList();

        var receiver = printed.Receiver;
        var associated = printed.AssociatedDocument;
        var (cardSurcharge, cardSurchargeLabel) = await CardSurchargeAsync(sale, printed.Amounts.Total, companyId, cancellationToken);

        return Result.Success(new SaleInvoicePrintResponse(
            Kind: query.Kind switch
            {
                SaleFiscalDocumentKind.Invoice => "invoice",
                SaleFiscalDocumentKind.DebitNote => "debitNote",
                _ => "creditNote"
            },
            Letter: type.Value.Letter,
            TypeCode: type.Value.Code,
            Title: type.Value.Title,
            PointOfSale: printed.PointOfSale,
            Number: printed.Number,
            Date: printed.Date,
            AuthorizationCode: printed.AuthorizationCode,
            AuthorizationExpiry: printed.AuthorizationExpiry,
            QrUrl: printed.QrUrl,
            IsVoided: document.Status == SaleInvoicingStatus.Voided,
            Issuer: new SaleInvoicePrintIssuer(
                printed.Issuer.LegalName,
                FiscalPrintLabels.FormatCuit(printed.Issuer.Cuit),
                FiscalPrintLabels.VatCondition(printed.Issuer.VatCondition),
                printed.Issuer.Iibb,
                printed.Issuer.ActivityStartDate,
                branch?.Address,
                branch?.Name),
            Receiver: new SaleInvoicePrintReceiver(
                string.IsNullOrWhiteSpace(receiver?.Name) ? "Consumidor Final" : receiver.Name,
                FiscalPrintLabels.VatCondition(receiver?.VatCondition ?? FiscalReceiverVatCondition.FinalConsumer),
                FiscalPrintLabels.Identification(receiver)),
            AssociatedDocument: associated is null
                ? null
                : new SaleInvoicePrintAssociatedDocument(
                    FiscalPrintLabels.DescribeType(associated.Type)?.Name ?? associated.Type,
                    associated.PointOfSale,
                    associated.Number,
                    associated.Date),
            SaleCode: sale.Code ?? sale.Id.Value.ToString(),
            SaleCondition: sale.IsCuentaCorriente ? "Cuenta corriente" : "Contado",
            VatRate: vat.FirstOrDefault()?.Rate ?? sale.VatRate ?? 21m,
            Items: items,
            Adjustments: new SaleInvoicePrintAdjustments(
                items.Sum(item => item.Total),
                sale.NoDeliverySurchargeTotal,
                sale.GeneralDiscountPercent,
                sale.ManualOverridePrice,
                cardSurcharge,
                cardSurchargeLabel),
            Amounts: new SaleInvoicePrintAmounts(
                printed.Amounts.Net,
                vat,
                printed.Amounts.Exempt,
                printed.Amounts.Total),
            Notes: sale.Notes));
    }

    /// <summary>
    /// Renglón del recargo de la tarjeta, solo si el comprobante autorizado lo incluye (total impreso − mercadería
    /// = recargo de la venta). Una venta facturada antes de cobrarse en cuotas imprime su factura como salió.
    /// </summary>
    private async Task<(decimal Amount, string? Label)> CardSurchargeAsync(
        Sale sale,
        decimal printedTotal,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var surcharge = sale.CardSurchargeTotal;
        var included = decimal.Round(printedTotal - sale.TotalAmount, 2, MidpointRounding.AwayFromZero);
        if (surcharge <= 0m || included != surcharge)
        {
            return (0m, null);
        }

        var card = sale.Payments.FirstOrDefault(payment => payment.Method == SalePaymentMethod.Card);
        var bank = card?.CardBankId is int bankId
            ? await _bankRepository.GetByIdAsync(bankId, companyId, cancellationToken)
            : null;

        return (surcharge, FiscalPrintLabels.CardSurcharge(bank?.Name, card?.CardCuotas, card?.CardSurchargePct));
    }
}
