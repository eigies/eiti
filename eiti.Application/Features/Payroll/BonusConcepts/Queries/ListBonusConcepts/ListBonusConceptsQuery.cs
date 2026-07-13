using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public sealed record ListBonusConceptsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<BonusConceptResponse>>>;
