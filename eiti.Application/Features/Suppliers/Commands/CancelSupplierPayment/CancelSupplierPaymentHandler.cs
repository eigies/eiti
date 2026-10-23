using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;
using eiti.Domain.Cash;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPayment;

// Anula un pago suelto. Si el pago comparte orden con otros medios se rechaza: se anula la orden entera.
// Si era el único medio de su orden, la orden queda anulada con él.
public sealed class CancelSupplierPaymentHandler : IRequestHandler<CancelSupplierPaymentCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISupplierPaymentOrderService _supplierPaymentOrderService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSupplierPaymentHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISupplierPaymentOrderService supplierPaymentOrderService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _supplierPaymentOrderService = supplierPaymentOrderService;
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

        var reversible = await _supplierPaymentOrderService.EnsureReversibleAloneAsync([payment], companyId, cancellationToken);
        if (reversible.IsFailure)
            return reversible;

        // Para efectivo hay que reintegrar a la caja; otros métodos no movieron efectivo.
        // Exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        CashSession? session = null;
        if (payment.Method == PurchasePaymentMethod.Cash)
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure(CancelSupplierPaymentErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure(CancelSupplierPaymentErrors.NoCashSessionOpen);
            session = resolve.Session;
        }

        await _supplierPaymentOrderService.ReversePaymentAsync(
            payment, supplier, session, companyId, userId, cancellationToken);
        _supplierRepository.Update(supplier);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
