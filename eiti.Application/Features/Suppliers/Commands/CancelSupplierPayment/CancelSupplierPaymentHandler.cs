using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;

public sealed class CancelSupplierPaymentHandler : IRequestHandler<CancelSupplierPaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSupplierPaymentHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        IPurchaseRepository purchaseRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IChequeRepository chequeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _purchaseRepository = purchaseRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _chequeRepository = chequeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelSupplierPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var payment = await _supplierPaymentRepository.GetByIdAsync(command.PaymentId, companyId.Value, cancellationToken);
        if (payment is null || payment.SupplierId != command.SupplierId)
            return Result.Failure(CancelSupplierPaymentErrors.PaymentNotFound);

        if (payment.Status == PurchasePaymentStatus.Cancelled)
            return Result.Failure(CancelSupplierPaymentErrors.AlreadyCancelled);

        var supplier = await _supplierRepository.GetByIdAsync(payment.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result.Failure(CancelSupplierPaymentErrors.PaymentNotFound);

        // Para efectivo hay que reintegrar a la caja; otros métodos no movieron efectivo.
        // Exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
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
                return Result.Failure(CancelSupplierPaymentErrors.NoAssignedCashDrawer);
            }

            if (session is null)
                return Result.Failure(CancelSupplierPaymentErrors.NoCashSessionOpen);
        }

        // Revertir las imputaciones FIFO que generó este pago (sus compras vuelven a Pendiente).
        var purchases = await _purchaseRepository.ListBySupplierPaymentIdAsync(companyId.Value, payment.Id, cancellationToken);
        var imputedTotal = 0m;
        foreach (var purchase in purchases)
        {
            var rows = purchase.Payments
                .Where(p => p.SupplierPaymentId == payment.Id && p.Status == PurchasePaymentStatus.Active)
                .ToList();
            foreach (var row in rows)
            {
                imputedTotal += row.Amount;
                purchase.CancelPayment(row.Id);
            }
        }

        // Si el pago genero saldo a favor, la anulacion lo revierte.
        var creditGeneratedByPayment = Math.Max(0m, payment.Amount - imputedTotal);
        if (creditGeneratedByPayment > 0m)
        {
            supplier.ConsumeCredit(creditGeneratedByPayment);
        }
        _supplierRepository.Update(supplier);

        // Reintegro de caja (solo efectivo) y devolución del cheque a cartera.
        session?.RegisterSupplierPaymentCancel(payment.Amount, payment.Id, userId, payment.Method);

        if (payment.Method == PurchasePaymentMethod.Check && payment.ChequeId.HasValue)
        {
            var cheque = await _chequeRepository.GetByIdAsync(payment.ChequeId.Value, companyId, cancellationToken);
            if (cheque is not null && cheque.Estado == ChequeStatus.Entregado)
                cheque.ReturnToCartera();
        }

        payment.Cancel();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
