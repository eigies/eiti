using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerPaymentLink;

public sealed class GetCustomerPaymentLinkHandler
    : IRequestHandler<GetCustomerPaymentLinkQuery, Result<GetCustomerPaymentLinkResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;

    public GetCustomerPaymentLinkHandler(
        ICurrentUserService currentUserService,
        ICustomerPaymentRepository customerPaymentRepository)
    {
        _currentUserService = currentUserService;
        _customerPaymentRepository = customerPaymentRepository;
    }

    public async Task<Result<GetCustomerPaymentLinkResponse>> Handle(
        GetCustomerPaymentLinkQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetCustomerPaymentLinkResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<GetCustomerPaymentLinkResponse>.Failure(
                Error.Unauthorized("Auth.IncompleteContext", "The current user context is incomplete."));
        }

        var payment = await _customerPaymentRepository.GetByIdAsync(
            request.PaymentId,
            companyId.Value,
            cancellationToken);

        if (payment is null)
        {
            return Result<GetCustomerPaymentLinkResponse>.Failure(
                Error.NotFound("CustomerPayments.NotFound", "No se encontro el cobro vinculado."));
        }

        return Result<GetCustomerPaymentLinkResponse>.Success(
            new GetCustomerPaymentLinkResponse(
                payment.Id,
                payment.CustomerId,
                payment.Amount,
                (int)payment.Method,
                (int)payment.Status,
                payment.Date));
    }
}
