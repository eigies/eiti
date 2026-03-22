using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListCcPayments;

public sealed class ListCcPaymentsHandler : IRequestHandler<ListCcPaymentsQuery, Result<IReadOnlyList<ListCcPaymentsItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;

    public ListCcPaymentsHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
    }

    public async Task<Result<IReadOnlyList<ListCcPaymentsItemResponse>>> Handle(ListCcPaymentsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ListCcPaymentsItemResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<IReadOnlyList<ListCcPaymentsItemResponse>>.Failure(
                Error.Unauthorized("Sales.ListCcPayments.Unauthorized", "The current user is not authenticated."));
        }

        var sale = await _saleRepository.GetByIdWithCcPaymentsAsync(new SaleId(request.SaleId), cancellationToken);
        if (sale is null || sale.CompanyId != companyId)
        {
            return Result<IReadOnlyList<ListCcPaymentsItemResponse>>.Failure(
                Error.NotFound("Sales.ListCcPayments.NotFound", "The sale was not found."));
        }

        var payments = sale.CcPayments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ListCcPaymentsItemResponse(
                p.Id.Value,
                p.SaleId.Value,
                (int)p.Method,
                p.Method.ToString(),
                p.Amount,
                p.Date,
                p.Notes,
                (int)p.Status,
                p.Status.ToString(),
                p.CreatedAt,
                p.CancelledAt))
            .ToList();

        return Result<IReadOnlyList<ListCcPaymentsItemResponse>>.Success(payments);
    }
}
