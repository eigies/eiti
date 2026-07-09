using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;

public sealed record ListDeductionConceptsQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<DeductionConceptResponse>>>;
