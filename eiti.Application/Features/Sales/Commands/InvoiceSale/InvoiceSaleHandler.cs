using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

public sealed class InvoiceSaleHandler : IRequestHandler<InvoiceSaleCommand, Result<InvoiceSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly ISaleInvoicingService _saleInvoicingService;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ISaleFiscalDocumentRepository fiscalDocuments,
        ISaleInvoicingService saleInvoicingService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _fiscalDocuments = fiscalDocuments;
        _saleInvoicingService = saleInvoicingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InvoiceSaleResponse>> Handle(InvoiceSaleCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<InvoiceSaleResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        if (!_saleInvoicingService.IsEnabled)
        {
            return Result.Failure<InvoiceSaleResponse>(InvoiceSaleErrors.NotConfigured);
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(command.Id), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result.Failure<InvoiceSaleResponse>(InvoiceSaleErrors.NotFound);
        }

        if (sale.SaleStatus == SaleStatus.Cancel)
        {
            return Result.Failure<InvoiceSaleResponse>(InvoiceSaleErrors.SaleCancelled);
        }

        var view = SaleInvoicingView.From(
            await _fiscalDocuments.ListBySaleAsync(sale.Id, companyId, cancellationToken));

        // Con factura vigente no se re-emite: para volver a facturar hay que anularla con una NC.
        if (view.LiveInvoice is not null)
        {
            return Result.Failure<InvoiceSaleResponse>(InvoiceSaleErrors.AlreadyInvoiced);
        }

        // Un intento en curso SÍ se puede reintentar, y es importante que se pueda: es el camino
        // de recuperación cuando se perdió la respuesta. El reintento re-envía el mismo RequestId,
        // así que el servicio devuelve el comprobante que ya emitió en vez de emitir otro.
        // Bloquearlo acá dejaba la venta trabada en "en trámite" para siempre.

        var outcome = await _saleInvoicingService.InvoiceAsync(sale, cancellationToken);

        // El estado se persiste SIEMPRE, haya salido bien o mal: si quedó Rechazado con el motivo,
        // el usuario lo ve en el detalle y puede reintentar.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome.IsSuccess
            ? Result.Success(Map(sale.Id.Value, outcome))
            : Result.Failure<InvoiceSaleResponse>(InvoiceSaleErrors.Rejected(outcome.Message));
    }

    private static InvoiceSaleResponse Map(Guid saleId, SaleInvoicingOutcome outcome)
    {
        var document = outcome.Document;

        return new InvoiceSaleResponse(
            saleId,
            (int)outcome.Status,
            outcome.Status.ToString(),
            document?.FiscalDocumentId,
            document?.DocumentType,
            document?.PointOfSale,
            document?.Number,
            document?.AuthorizationCode,
            document?.AuthorizationExpiry,
            document?.QrUrl,
            outcome.Message);
    }
}
