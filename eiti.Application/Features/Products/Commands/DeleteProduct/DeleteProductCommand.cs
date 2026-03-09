using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;
