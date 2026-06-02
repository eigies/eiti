using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.AddPurchasePayment;

public sealed class AddPurchasePaymentHandler : IRequestHandler<AddPurchasePaymentCommand, Result<AddPurchasePaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddPurchasePaymentHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddPurchasePaymentResponse>> Handle(AddPurchasePaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddPurchasePaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        if (!Enum.IsDefined(typeof(PurchasePaymentMethod), command.Method))
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.InvalidPaymentMethod);

        if (command.Amount <= 0)
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.InvalidAmount);

        var purchase = await _purchaseRepository.GetByIdAsync(command.PurchaseId, companyId.Value, cancellationToken);
        if (purchase is null)
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.NotFound);

        if (purchase.Status == PurchaseStatus.Cancelled)
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.PurchaseCancelled);

        if (purchase.Status == PurchaseStatus.Paid)
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.PurchaseAlreadyPaid);

        var method = (PurchasePaymentMethod)command.Method;

        // Always prefer the user's assigned drawer; fall back to any open session only for users with CashDrawerViewAll and no assigned drawer.
        var assignedDrawer = await _cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);

        CashSession? session;
        if (assignedDrawer is not null)
        {
            session = await _cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);
            if (session is null)
                return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.NoCashSessionOpen);
        }
        else if (_currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
        {
            session = await _cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
            if (session is null)
                return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.NoCashSessionOpen);
        }
        else
        {
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.NoAssignedCashDrawer);
        }

        if (IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.CashSessionFromPreviousDay);

        // Sobrepago: el excedente queda como saldo a favor del proveedor.
        var remaining = purchase.PendingAmount;
        var excess = Math.Max(0m, command.Amount - remaining);
        if (excess > 0 && purchase.SupplierId is null)
            return Result<AddPurchasePaymentResponse>.Failure(AddPurchasePaymentErrors.OverpaymentRequiresSupplier);

        var payment = PurchasePayment.Create(method, command.Amount, command.Date, command.Reference, command.Notes);
        purchase.AddPayment(payment);
        session!.RegisterPurchaseExpense(command.Amount, purchase.Id, userId, method);

        Supplier? supplier = null;
        if (excess > 0 && purchase.SupplierId.HasValue)
        {
            supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId.Value, companyId.Value, cancellationToken);
            if (supplier is not null)
            {
                supplier.AddCredit(excess);
                _supplierRepository.Update(supplier);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AddPurchasePaymentResponse>.Success(new AddPurchasePaymentResponse(
            purchase.Id,
            (int)purchase.Status,
            purchase.Status.ToString(),
            purchase.TotalAmount,
            purchase.TaxAmount,
            purchase.GrandTotal,
            purchase.TotalPaid,
            Math.Max(0m, purchase.PendingAmount),
            payment.Id,
            excess,
            supplier?.CreditBalance ?? 0m));
    }

    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    private static bool IsFromPreviousBusinessDay(DateTime openedAtUtc)
    {
        var openedLocal = openedAtUtc + ArgentinaOffset;
        var nowLocal = DateTime.UtcNow + ArgentinaOffset;
        return openedLocal.Date < nowLocal.Date;
    }
}
