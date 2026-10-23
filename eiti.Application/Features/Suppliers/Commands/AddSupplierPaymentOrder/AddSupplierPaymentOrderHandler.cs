using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

// Orden de pago: varios medios (cheques de cartera, transferencia, efectivo) en un solo comprobante.
// Todo o nada: un solo SaveChanges al final.
public sealed class AddSupplierPaymentOrderHandler
    : IRequestHandler<AddSupplierPaymentOrderCommand, Result<AddSupplierPaymentOrderResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISupplierPaymentOrderService _supplierPaymentOrderService;
    private readonly IUnitOfWork _unitOfWork;

    public AddSupplierPaymentOrderHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISupplierPaymentOrderService supplierPaymentOrderService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _supplierPaymentOrderService = supplierPaymentOrderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddSupplierPaymentOrderResponse>> Handle(
        AddSupplierPaymentOrderCommand command,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddSupplierPaymentOrderResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<AddSupplierPaymentOrderResponse>.Failure(AddSupplierPaymentOrderErrors.SupplierNotFound);

        // Resolver caja abierta: exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<AddSupplierPaymentOrderResponse>.Failure(resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                ? AddSupplierPaymentOrderErrors.NoAssignedCashDrawer
                : AddSupplierPaymentOrderErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddSupplierPaymentOrderResponse>.Failure(AddSupplierPaymentOrderErrors.CashSessionFromPreviousDay);

        var outcome = await _supplierPaymentOrderService.CreateAsync(
            supplier,
            session,
            command.Date,
            command.Notes,
            command.Lines
                .Select(l => new SupplierPaymentLineInput(l.Method, l.Amount, l.ChequeId, l.Reference))
                .ToList(),
            companyId,
            userId,
            cancellationToken);
        if (outcome.IsFailure)
            return Result<AddSupplierPaymentOrderResponse>.Failure(outcome.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = outcome.Value;
        return Result<AddSupplierPaymentOrderResponse>.Success(new AddSupplierPaymentOrderResponse(
            created.Order.Id,
            created.Order.Code,
            supplier.Id,
            created.Order.TotalAmount,
            created.Payments.Select(p => p.Id).ToList(),
            created.AppliedToPurchases,
            created.CreditAdded,
            created.SupplierCreditBalance,
            created.Imputaciones));
    }
}
