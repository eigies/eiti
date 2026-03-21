using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.SearchDeliveryAddresses;

public sealed class SearchDeliveryAddressesHandler : IRequestHandler<SearchDeliveryAddressesQuery, Result<IReadOnlyList<string>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;

    public SearchDeliveryAddressesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
    }

    public async Task<Result<IReadOnlyList<string>>> Handle(SearchDeliveryAddressesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<string>>.Failure(authCheck.Error);

        if (string.IsNullOrWhiteSpace(request.Query))
            return Result<IReadOnlyList<string>>.Success([]);

        var results = await _saleRepository.SearchDeliveryAddressesAsync(
            request.Query.Trim(),
            _currentUserService.CompanyId,
            cancellationToken: cancellationToken);

        return Result<IReadOnlyList<string>>.Success(results);
    }
}
