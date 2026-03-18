using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.SearchCustomers;

public sealed class SearchCustomersHandler : IRequestHandler<SearchCustomersQuery, Result<IReadOnlyList<SearchCustomersItemResponse>>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICurrentUserService _currentUserService;

    public SearchCustomersHandler(ICustomerRepository customerRepository, ICurrentUserService currentUserService)
    {
        _customerRepository = customerRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<SearchCustomersItemResponse>>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<SearchCustomersItemResponse>>.Failure(authCheck.Error);

        var customers = await _customerRepository.SearchAsync(
            _currentUserService.CompanyId,
            request.Query,
            request.Email,
            request.DocumentNumber,
            cancellationToken);

        return Result<IReadOnlyList<SearchCustomersItemResponse>>.Success(
            customers.Select(customer => new SearchCustomersItemResponse(
                    customer.Id.Value,
                    customer.Name,
                    customer.FullName,
                    customer.Email.Value,
                    customer.Phone,
                    customer.DocumentType.HasValue ? (int)customer.DocumentType.Value : null,
                    customer.DocumentType?.ToString(),
                    customer.DocumentNumber,
                    customer.TaxId))
                .ToList());
    }
}
