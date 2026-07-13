using eiti.Application.Common;
using eiti.Domain.Banks;
using MediatR;

namespace eiti.Application.Features.Banks.Queries.ListBanks;

public sealed record ListBanksQuery(bool ActiveOnly, BankUsage Usage = BankUsage.All)
    : IRequest<Result<IReadOnlyList<BankResponse>>>;
