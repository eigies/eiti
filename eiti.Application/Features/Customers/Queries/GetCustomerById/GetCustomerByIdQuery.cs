using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id)
    : IRequest<Result<GetCustomerByIdResponse>>;
