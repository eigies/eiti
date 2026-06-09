using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CancelPurchasePayment;

public sealed class CancelPurchasePaymentHandler : IRequestHandler<CancelPurchasePaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchasePaymentHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelPurchasePaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

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

        // Si el pago fue en efectivo, hay que reintegrar la plata a la caja: cuando se registró el pago
        // se generó un egreso (PurchaseExpense, dirección Out) que bajó el esperado del cajón. Anularlo
        // sin el reverso dejaría la caja "corta". Resolvemos la sesión abierta igual que en el alta del
        // pago (caja asignada del usuario, o cualquier abierta si tiene ver-todas). Para métodos no-efectivo
        // (transferencia/cheque/otro) el alta no movió efectivo, así que no hay nada que reintegrar.
        var reversedAmount = payment.Amount;
        CashSession? session = null;

        if (payment.Method == PurchasePaymentMethod.Cash)
        {
            var assignedDrawer = await _cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);
            if (assignedDrawer is not null)
            {
                session = await _cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);
            }
            else if (_currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
            {
                session = await _cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
            }
            else
            {
                return Result.Failure(CancelPurchasePaymentErrors.NoAssignedCashDrawer);
            }

            if (session is null)
                return Result.Failure(CancelPurchasePaymentErrors.NoCashSessionOpen);
        }

        try
        {
            purchase.CancelPayment(command.PaymentId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict("Purchases.CancelPayment.Error", ex.Message));
        }

        session?.RegisterPurchasePaymentCancel(reversedAmount, purchase.Id, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
