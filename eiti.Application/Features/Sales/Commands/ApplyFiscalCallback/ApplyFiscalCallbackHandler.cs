using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.ApplyFiscalCallback;

public sealed class ApplyFiscalCallbackHandler : IRequestHandler<ApplyFiscalCallbackCommand, Result>
{
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly IFiscalizationService _fiscalizationService;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyFiscalCallbackHandler(
        ISaleFiscalDocumentRepository fiscalDocuments,
        IFiscalizationService fiscalizationService,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocuments = fiscalDocuments;
        _fiscalizationService = fiscalizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ApplyFiscalCallbackCommand command, CancellationToken cancellationToken)
    {
        var document = await _fiscalDocuments.GetByFiscalDocumentIdAsync(command.DocumentId, cancellationToken);

        // Respaldo: si se perdió la respuesta del pedido original nunca se registró el id del
        // servicio, así que por ese id no hay match. El requestId que devuelve el callback ES
        // nuestra clave de idempotencia, y con eso sí se encuentra la fila.
        if (document is null && SaleFiscalDocument.TryParseRequestId(command.SaleId, out var saleId, out var kind, out var sequence))
        {
            document = await _fiscalDocuments.GetByRequestAsync(saleId, kind, sequence, cancellationToken);
        }

        if (document is null)
        {
            // No es un error del servicio fiscal: puede ser una venta borrada. Se responde OK
            // igual para que no reintente ocho veces algo que nunca va a resolver.
            return Result.Success();
        }

        var authorized = string.Equals(command.Status, "authorized", StringComparison.OrdinalIgnoreCase);

        if (!authorized)
        {
            document.Reject(command.DocumentId, command.RejectionReason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // El callback no trae tipo, punto de venta ni vencimiento del CAE, y los tres hacen falta
        // para poder emitir después la nota de crédito asociada. Se leen del servicio.
        // El tenant sale del documento que ya localizamos, no del callback: el cuerpo del
        // callback es input externo y no debe elegir de qué empresa se leen datos.
        var detail = await _fiscalizationService.GetDocumentAsync(
            document.CompanyId.Value, command.DocumentId, cancellationToken);

        document.Authorize(
            command.DocumentId,
            detail.DocumentType,
            detail.PointOfSale,
            command.Number ?? detail.Number,
            command.AuthorizationCode ?? detail.AuthorizationCode,
            detail.ValidUntil,
            command.Qr ?? detail.QrUrl,
            DateTime.UtcNow);

        await SaleFiscalDocumentEffects.ApplyAsync(document, _fiscalDocuments, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
