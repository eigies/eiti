using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public sealed class ConvertQuoteToSaleHandler : IRequestHandler<ConvertQuoteToSaleCommand, Result<CreateCcSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;

    public ConvertQuoteToSaleHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        ISender sender,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _sender = sender;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateCcSaleResponse>> Handle(
        ConvertQuoteToSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateCcSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quote = await _quoteRepository.GetByIdAsync(new QuoteId(request.QuoteId), companyId, cancellationToken);
        if (quote is null)
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.QuoteNotFound);
        }

        if (quote.Status != QuoteStatus.Pending)
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.NotPending);
        }

        var now = DateTime.UtcNow;
        if (quote.IsExpired(now))
        {
            return Result<CreateCcSaleResponse>.Failure(ConvertQuoteToSaleErrors.Expired);
        }

        // "Con IVA": los precios del presupuesto son netos, así que se llevan a FINAL (neto × (1+tasa)) antes
        // de crear la venta; el total queda con IVA incluido y la venta guarda el desglose (VatRate/VatAmount).
        // "Sin IVA": bajan los netos tal cual y no se registra IVA. Exento (tasa 0) equivale a sin IVA.
        var applyVat = request.WithVat && quote.VatRate > 0m;
        var saleDetails = applyVat
            ? request.Details
                .Select(detail => detail.UnitPrice.HasValue
                    ? detail with { UnitPrice = decimal.Round(detail.UnitPrice.Value * (1m + quote.VatRate / 100m), 2, MidpointRounding.AwayFromZero) }
                    : detail)
                .ToList()
            : request.Details;

        var createSaleResult = await _sender.Send(
            new CreateCcSaleCommand(
                request.BranchId,
                request.CustomerId,
                saleDetails,
                request.TradeIns,
                request.GeneralDiscountPercent,
                request.ManualOverridePrice,
                applyVat ? quote.VatRate : null),
            cancellationToken);

        if (createSaleResult.IsFailure)
        {
            return Result<CreateCcSaleResponse>.Failure(createSaleResult.Error);
        }

        quote.MarkConverted(createSaleResult.Value.Id, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createSaleResult;
    }
}
