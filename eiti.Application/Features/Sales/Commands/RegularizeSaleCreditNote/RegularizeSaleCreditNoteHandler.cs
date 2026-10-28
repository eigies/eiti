using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;

public sealed class RegularizeSaleCreditNoteHandler
    : IRequestHandler<RegularizeSaleCreditNoteCommand, Result<RegularizeSaleCreditNoteResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleInvoicingService _saleInvoicingService;
    private readonly IUnitOfWork _unitOfWork;

    public RegularizeSaleCreditNoteHandler(
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

    public async Task<Result<RegularizeSaleCreditNoteResponse>> Handle(
        RegularizeSaleCreditNoteCommand command,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        if (!_saleInvoicingService.IsEnabled)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(RegularizeSaleCreditNoteErrors.NotConfigured);
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(command.Id), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(RegularizeSaleCreditNoteErrors.NotFound);
        }

        // Los receptores salen del servicio fiscal: sin ellos no se sabe si hay algo mal emitido ni a quién.
        var receivers = await _saleInvoicingService.GetFiscalReceiversAsync(sale, cancellationToken);
        if (!receivers.IsAvailable)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(
                RegularizeSaleCreditNoteErrors.Unavailable(receivers.UnavailableReason));
        }

        if (receivers.RegularizationStep == SaleCreditNoteRegularizationStep.None)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(RegularizeSaleCreditNoteErrors.NothingToRegularize);
        }

        if (!command.Confirm)
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(RegularizeSaleCreditNoteErrors.ConfirmationRequired);
        }

        var regularization = await _saleInvoicingService.RegularizeCreditNoteAsync(sale, receivers, cancellationToken);

        // Lo emitido se persiste siempre: si se cortó a mitad de camino, el próximo intento retoma desde acá.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // En trámite (encolado) no es un error: lo termina el callback y se vuelve a regularizar para seguir.
        var inProgress = regularization.CreditNote ?? regularization.DebitNote;
        if (!regularization.Completed && inProgress is not { IsSuccess: true })
        {
            return Result.Failure<RegularizeSaleCreditNoteResponse>(
                RegularizeSaleCreditNoteErrors.Rejected(regularization.Message));
        }

        return Result.Success(new RegularizeSaleCreditNoteResponse(
            sale.Id.Value,
            regularization.Completed,
            Map(regularization.DebitNote),
            Map(regularization.CreditNote),
            regularization.Message));
    }

    private static RegularizedDocumentResponse? Map(SaleInvoicingOutcome? outcome) =>
        outcome is null
            ? null
            : new RegularizedDocumentResponse(
                (int)outcome.Status,
                outcome.Status.ToString(),
                outcome.Document?.PointOfSale,
                outcome.Document?.Number);
}
