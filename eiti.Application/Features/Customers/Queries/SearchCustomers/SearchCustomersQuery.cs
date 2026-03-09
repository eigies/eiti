using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.SearchCustomers;

public sealed record SearchCustomersQuery(
    string? Query,
    string? Email,
    string? DocumentNumber
) : IRequest<Result<IReadOnlyList<SearchCustomersItemResponse>>>;

public sealed record SearchCustomersItemResponse(
    Guid Id,
    string Name,
    string FullName,
    string Email,
    string Phone,
    int? DocumentType,
    string? DocumentTypeName,
    string? DocumentNumber,
    string? TaxId);
