using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Banks.Queries.ListBanks;

public sealed record ListBanksQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<BankResponse>>>;
