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
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<SearchCustomersItemResponse>>.Failure(
                Error.Unauthorized("Customer.Search.Unauthorized", "El usuario actual no esta autenticado."));
        }

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
