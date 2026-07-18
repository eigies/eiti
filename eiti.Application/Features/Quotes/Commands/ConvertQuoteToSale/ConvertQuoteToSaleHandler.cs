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

        var createSaleResult = await _sender.Send(
            new CreateCcSaleCommand(
                request.BranchId,
                request.CustomerId,
                request.Details,
                request.TradeIns,
                request.GeneralDiscountPercent,
                request.ManualOverridePrice),
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
