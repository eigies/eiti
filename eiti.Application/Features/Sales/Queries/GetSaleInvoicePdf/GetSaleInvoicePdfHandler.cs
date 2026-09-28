using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePdf;

public sealed class GetSaleInvoicePdfHandler
    : IRequestHandler<GetSaleInvoicePdfQuery, Result<SaleInvoicePdfResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly IFiscalizationService _fiscalizationService;

    public GetSaleInvoicePdfHandler(
        ICurrentUserService currentUserService,
        ISaleFiscalDocumentRepository fiscalDocuments,
        IFiscalizationService fiscalizationService)
    {
        _currentUserService = currentUserService;
        _fiscalDocuments = fiscalDocuments;
        _fiscalizationService = fiscalizationService;
    }

    public async Task<Result<SaleInvoicePdfResponse>> Handle(
        GetSaleInvoicePdfQuery query,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result.Failure<SaleInvoicePdfResponse>(authCheck.Error);
        }

        var companyId = _currentUserService.CompanyId!;

        // El scoping por empresa se chequea acá y no en el servicio fiscal: es EITI quien sabe
        // de qué empresa es esta venta.
        var documents = await _fiscalDocuments.ListBySaleAsync(new SaleId(query.SaleId), companyId, cancellationToken);

        if (documents.Count == 0)
        {
            return Result.Failure<SaleInvoicePdfResponse>(GetSaleInvoicePdfErrors.NotFound);
        }

        // Se permite descargar también una factura ANULADA: existió, tuvo CAE y es un documento
        // legal que hay que poder reimprimir. Que esté anulada lo dice el estado, no la ausencia
        // del PDF.
        var document = SaleInvoicingView.From(documents).PrintableInvoice;

        if (document is null || document.FiscalDocumentId is null)
        {
            return Result.Failure<SaleInvoicePdfResponse>(GetSaleInvoicePdfErrors.NotInvoiced);
        }

        var pdf = await _fiscalizationService.DownloadPdfAsync(
            companyId.Value, document.FiscalDocumentId.Value, cancellationToken);
        if (!pdf.IsSuccess || pdf.Content is null)
        {
            return Result.Failure<SaleInvoicePdfResponse>(GetSaleInvoicePdfErrors.Unavailable(pdf.ErrorMessage));
        }

        var fileName = document.PointOfSale is not null && document.Number is not null
            ? $"comprobante-{document.PointOfSale:D5}-{document.Number:D8}.pdf"
            : $"comprobante-{query.SaleId}.pdf";

        return Result.Success(new SaleInvoicePdfResponse(pdf.Content, fileName));
    }
}
