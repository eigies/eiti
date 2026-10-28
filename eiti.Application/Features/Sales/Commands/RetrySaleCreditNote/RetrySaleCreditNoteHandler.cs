using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;

public sealed class RetrySaleCreditNoteHandler
    : IRequestHandler<RetrySaleCreditNoteCommand, Result<RetrySaleCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleInvoicingService _saleInvoicingService;
    private readonly IUnitOfWork _unitOfWork;

    public RetrySaleCreditNoteHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ISaleInvoicingService saleInvoicingService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _saleInvoicingService = saleInvoicingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RetrySaleCreditNoteResponse>> Handle(
        RetrySaleCreditNoteCommand command,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        if (!_saleInvoicingService.IsEnabled)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.NotConfigured);
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(command.Id), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.NotFound);
        }

        // La NC anula la factura de una venta anulada; sobre una venta vigente se anula la venta.
        if (sale.SaleStatus != SaleStatus.Cancel)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.SaleNotCancelled);
        }

        // Sin saber a quién se emitió la factura no se emite la NC: podría salir a otro receptor.
        var receivers = await _saleInvoicingService.GetFiscalReceiversAsync(sale, cancellationToken);
        if (!receivers.IsAvailable)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.Unavailable(receivers.UnavailableReason));
        }

        if (!receivers.InvoiceIsLive)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.NothingToReverse);
        }

        if (!receivers.CustomerMatchesInvoice && !command.ConfirmInvoiceReceiver)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.InvoiceReceiverChanged);
        }

        var outcome = await _saleInvoicingService.IssueCreditNoteAsync(sale, cancellationToken);

        // El estado se persiste siempre: un rechazo queda con su motivo en el detalle para reintentar.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!outcome.IsSuccess)
        {
            return Result.Failure<RetrySaleCreditNoteResponse>(RetrySaleCreditNoteErrors.Rejected(outcome.Message));
        }

        return Result.Success(new RetrySaleCreditNoteResponse(
            sale.Id.Value,
            (int)outcome.Status,
            outcome.Status.ToString(),
            outcome.Document?.PointOfSale,
            outcome.Document?.Number,
            outcome.Message));
    }
}
