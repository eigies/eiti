using System.Globalization;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleFiscalReceivers;

public sealed class GetSaleFiscalReceiversHandler
    : IRequestHandler<GetSaleFiscalReceiversQuery, Result<GetSaleFiscalReceiversResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleInvoicingService _saleInvoicingService;

    public GetSaleFiscalReceiversHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ISaleInvoicingService saleInvoicingService)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleInvoicingService = saleInvoicingService;
    }

    public async Task<Result<GetSaleFiscalReceiversResponse>> Handle(
        GetSaleFiscalReceiversQuery query,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<GetSaleFiscalReceiversResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        // El repositorio de ventas no filtra por empresa: el aislamiento se chequea acá.
        var sale = await _saleRepository.GetByIdAsync(new SaleId(query.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure<GetSaleFiscalReceiversResponse>(GetSaleFiscalReceiversErrors.NotFound);
        }

        var receivers = await _saleInvoicingService.GetFiscalReceiversAsync(sale, cancellationToken);

        return Result.Success(new GetSaleFiscalReceiversResponse(
            receivers.IsAvailable,
            receivers.UnavailableReason,
            receivers.InvoicePrinted is null ? null : Map(receivers.InvoicePrinted.Receiver, DocumentLabel(receivers.InvoicePrinted)),
            Map(receivers.CustomerReceiver, null),
            receivers.CustomerMatchesInvoice,
            receivers.CreditNotePrinted is null ? null : Map(receivers.CreditNotePrinted.Receiver, DocumentLabel(receivers.CreditNotePrinted)),
            receivers.CreditNoteMatchesInvoice,
            receivers.RegularizationStep switch
            {
                SaleCreditNoteRegularizationStep.DebitNote => "debitNote",
                SaleCreditNoteRegularizationStep.CreditNote => "creditNote",
                _ => "none"
            },
            receivers.DebitNotePrinted is null ? null : Map(receivers.DebitNotePrinted.Receiver, DocumentLabel(receivers.DebitNotePrinted))));
    }

    private static SaleFiscalReceiverResponse Map(FiscalReceiver? receiver, string? document) =>
        new(
            document,
            string.IsNullOrWhiteSpace(receiver?.Name) ? "Consumidor Final" : receiver.Name,
            FiscalPrintLabels.VatCondition(receiver?.VatCondition ?? FiscalReceiverVatCondition.FinalConsumer),
            FiscalPrintLabels.Identification(receiver));

    private static string DocumentLabel(FiscalPrintableDocument printed) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{FiscalPrintLabels.DescribeType(printed.Type)?.Name ?? printed.Type} {printed.PointOfSale:D5}-{printed.Number:D8}");
}
