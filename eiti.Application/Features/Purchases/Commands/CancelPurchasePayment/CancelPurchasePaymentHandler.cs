using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchasePayment;

public sealed class CancelPurchasePaymentHandler : IRequestHandler<CancelPurchasePaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchasePaymentHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelPurchasePaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var purchase = await _purchaseRepository.GetByIdAsync(command.PurchaseId, companyId.Value, cancellationToken);
        if (purchase is null)
            return Result.Failure(CancelPurchasePaymentErrors.PurchaseNotFound);

        if (purchase.Status == PurchaseStatus.Cancelled)
            return Result.Failure(CancelPurchasePaymentErrors.PurchaseCancelled);

        var payment = purchase.Payments.FirstOrDefault(p => p.Id == command.PaymentId);
        if (payment is null)
            return Result.Failure(CancelPurchasePaymentErrors.PaymentNotFound);

        if (payment.Status == PurchasePaymentStatus.Cancelled)
            return Result.Failure(CancelPurchasePaymentErrors.PaymentAlreadyCancelled);

        try
        {
            purchase.CancelPayment(command.PaymentId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict("Purchases.CancelPayment.Error", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
