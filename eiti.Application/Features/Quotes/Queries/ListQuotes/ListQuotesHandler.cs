using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed class ListQuotesHandler : IRequestHandler<ListQuotesQuery, Result<IReadOnlyList<QuoteListItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuoteRepository _quoteRepository;
    private readonly ICustomerRepository _customerRepository;

    public ListQuotesHandler(
        ICurrentUserService currentUserService,
        IQuoteRepository quoteRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _quoteRepository = quoteRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<IReadOnlyList<QuoteListItemResponse>>> Handle(
        ListQuotesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<QuoteListItemResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var quotes = await _quoteRepository.ListAsync(
            companyId, request.Status, request.DateFrom, request.DateTo, request.CustomerId, cancellationToken);

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            quotes = quotes.Where(quote => allowed.Contains(quote.BranchId.Value)).ToList();
        }

        var customerIds = quotes
            .Where(quote => quote.CustomerId is not null)
            .Select(quote => quote.CustomerId!.Value)
            .Distinct()
            .ToList();

        var customerMap = new Dictionary<Guid, string>();
        foreach (var customerId in customerIds)
        {
            var customer = await _customerRepository.GetByIdAsync(new CustomerId(customerId), companyId, cancellationToken);
            if (customer is not null)
            {
                customerMap[customerId] = customer.FullName;
            }
        }

        var now = DateTime.UtcNow;
        return Result<IReadOnlyList<QuoteListItemResponse>>.Success(
            quotes.Select(quote => new QuoteListItemResponse(
                quote.Id.Value,
                quote.Code,
                quote.BranchId.Value,
                quote.CustomerId?.Value,
                quote.CustomerId is not null && customerMap.TryGetValue(quote.CustomerId.Value, out var name) ? name : null,
                quote.ProspectName,
                quote.TotalAmount,
                quote.ExpiresAt,
                (int)quote.Status,
                quote.Status.ToString(),
                quote.IsExpired(now),
                quote.ConvertedSaleId,
                quote.CreatedAt)).ToList());
    }
}
