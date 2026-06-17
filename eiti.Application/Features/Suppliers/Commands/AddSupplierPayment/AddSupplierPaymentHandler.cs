using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public sealed class AddSupplierPaymentHandler : IRequestHandler<AddSupplierPaymentCommand, Result<AddSupplierPaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddSupplierPaymentHandler(
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

    public async Task<Result<AddSupplierPaymentResponse>> Handle(AddSupplierPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddSupplierPaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        if (!Enum.IsDefined(typeof(PurchasePaymentMethod), command.Method))
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.InvalidPaymentMethod);

        if (command.Amount <= 0)
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.InvalidAmount);

        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.SupplierNotFound);

        var method = (PurchasePaymentMethod)command.Method;

        // Pago con cheque: referencia un cheque en cartera y se usa por su valor completo (sin particionar).
        Cheque? cheque = null;
        var amount = command.Amount;
        if (method == PurchasePaymentMethod.Check)
        {
            if (command.ChequeId is null)
                return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.ChequeRequired);

            cheque = await _chequeRepository.GetByIdAsync(command.ChequeId.Value, companyId, cancellationToken);
            if (cheque is null)
                return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.ChequeNotFound);

            if (cheque.Estado != ChequeStatus.EnCartera)
                return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.ChequeNotEnCartera);

            amount = cheque.Monto;
        }

        // Resolver caja abierta: exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        var assignedDrawer = await _cashDrawerRepository.GetByAssignedUserAsync(userId, companyId, cancellationToken);
        CashSession? session;
        if (assignedDrawer is not null)
        {
            session = await _cashSessionRepository.GetOpenByDrawerAsync(assignedDrawer.Id, companyId, cancellationToken);
            if (session is null)
                return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.NoCashSessionOpen);
        }
        else if (_currentUserService.HasPermission(PermissionCodes.CashDrawerViewAll))
        {
            session = await _cashSessionRepository.GetAnyOpenByCompanyAsync(companyId, cancellationToken);
            if (session is null)
                return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.NoCashSessionOpen);
        }
        else
        {
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.NoAssignedCashDrawer);
        }

        if (IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.CashSessionFromPreviousDay);

        var creditBefore = supplier.CreditBalance;

        var payment = SupplierPayment.Create(
            companyId.Value,
            supplier.Id,
            session.BranchId.Value,
            method,
            amount,
            command.Date,
            command.Reference,
            command.Notes,
            userId.Value,
            cheque?.Id);

        await _supplierPaymentRepository.AddAsync(payment, cancellationToken);

        // Caja: el cheque/transferencia no mueven efectivo (dirección None); el efectivo sí.
        session.RegisterSupplierPaymentExpense(amount, payment.Id, userId, method);

        // El cheque sale de cartera (Entregado al proveedor).
        cheque?.EndorseToSupplier();

        // Imputación FIFO a las compras pendientes del proveedor; el excedente queda como saldo a favor.
        supplier.AddCredit(amount);
        await SupplierCreditApplicator.ApplyToPendingPurchasesAsync(
            supplier, companyId.Value, _purchaseRepository, null, cancellationToken, payment.Id);
        _supplierRepository.Update(supplier);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var creditAfter = supplier.CreditBalance;
        var appliedToPurchases = Math.Max(0m, creditBefore + amount - creditAfter);
        var creditAdded = Math.Max(0m, creditAfter - creditBefore);

        return Result<AddSupplierPaymentResponse>.Success(new AddSupplierPaymentResponse(
            payment.Id,
            supplier.Id,
            amount,
            appliedToPurchases,
            creditAdded,
            creditAfter));
    }

    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    private static bool IsFromPreviousBusinessDay(DateTime openedAtUtc)
    {
        var openedLocal = openedAtUtc + ArgentinaOffset;
        var nowLocal = DateTime.UtcNow + ArgentinaOffset;
        return openedLocal.Date < nowLocal.Date;
    }
}
