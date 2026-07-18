using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CancelQuote;

public sealed class CancelQuoteHandler : IRequestHandler<CancelQuoteCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelQuoteHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelQuoteCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return authCheck;

        var quote = await _quoteRepository.GetByIdAsync(
            new QuoteId(request.QuoteId), _currentUserService.CompanyId!, cancellationToken);
        if (quote is null)
        {
            return Result.Failure(CancelQuoteErrors.QuoteNotFound);
        }

        try
        {
            quote.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict("Quotes.Cancel.InvalidState", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
