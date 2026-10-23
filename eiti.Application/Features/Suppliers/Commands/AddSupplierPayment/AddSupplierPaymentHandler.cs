using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

// Pago suelto de un solo medio. Se registra como una orden de pago de un medio, así todo pago nuevo
// tiene número de OP y el extracto los muestra igual.
public sealed class AddSupplierPaymentHandler : IRequestHandler<AddSupplierPaymentCommand, Result<AddSupplierPaymentResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISupplierPaymentOrderService _supplierPaymentOrderService;
    private readonly IUnitOfWork _unitOfWork;

    public AddSupplierPaymentHandler(
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

    public async Task<Result<AddSupplierPaymentResponse>> Handle(AddSupplierPaymentCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<AddSupplierPaymentResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.SupplierNotFound);

        // Resolver caja abierta: exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
            _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
        if (resolve.Status != CashSessionResolveStatus.Resolved)
            return Result<AddSupplierPaymentResponse>.Failure(resolve.Status == CashSessionResolveStatus.NoAssignedDrawer
                ? AddSupplierPaymentErrors.NoAssignedCashDrawer
                : AddSupplierPaymentErrors.NoCashSessionOpen);
        var session = resolve.Session!;

        if (BusinessDay.IsFromPreviousBusinessDay(session.OpenedAt))
            return Result<AddSupplierPaymentResponse>.Failure(AddSupplierPaymentErrors.CashSessionFromPreviousDay);

        var outcome = await _supplierPaymentOrderService.CreateAsync(
            supplier,
            session,
            command.Date,
            command.Notes,
            [new SupplierPaymentLineInput(command.Method, command.Amount, command.ChequeId, command.Reference)],
            companyId,
            userId,
            cancellationToken);
        if (outcome.IsFailure)
            return Result<AddSupplierPaymentResponse>.Failure(outcome.Error);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = outcome.Value;
        var payment = created.Payments[0];
        return Result<AddSupplierPaymentResponse>.Success(new AddSupplierPaymentResponse(
            payment.Id,
            supplier.Id,
            payment.Amount,
            created.AppliedToPurchases,
            created.CreditAdded,
            created.SupplierCreditBalance,
            created.Imputaciones));
    }
}
