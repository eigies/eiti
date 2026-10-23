using eiti.Application.Abstractions.Repositories;
using eiti.Application.Common;
using eiti.Application.Features.Purchases.Common;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using eiti.Domain.Users;

namespace eiti.Application.Features.Suppliers.Common;

// Un medio dentro de un pago al proveedor. Con cheque, el monto es el del cheque (se entrega entero).
public sealed record SupplierPaymentLineInput(
    int Method,
    decimal Amount,
    Guid? ChequeId,
    string? Reference);

public sealed record SupplierPaymentOrderOutcome(
    SupplierPaymentOrder Order,
    IReadOnlyList<SupplierPayment> Payments,
    IReadOnlyList<SupplierPaymentImputacion> Imputaciones,
    decimal AppliedToPurchases,
    decimal CreditAdded,
    decimal SupplierCreditBalance);

/// <summary>
/// Registra un pago al proveedor con uno o más medios bajo una orden de pago (OP-####). Cada medio es un
/// <see cref="SupplierPayment"/> completo: asiento de caja (solo el efectivo mueve el esperado), cheque de
/// cartera entregado e imputación FIFO a las compras pendientes. Lo usan el alta de pago suelto (orden de
/// un medio) y la orden de pago. Valida todos los medios antes de tocar nada; no llama SaveChanges.
/// </summary>
public interface ISupplierPaymentOrderService
{
    Task<Result<SupplierPaymentOrderOutcome>> CreateAsync(
        Supplier supplier,
        CashSession session,
        DateTime date,
        string? notes,
        IReadOnlyList<SupplierPaymentLineInput> lines,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);

    /// <summary>Anula la orden entera: revierte cada medio activo y la marca anulada.</summary>
    Task CancelOrderAsync(
        SupplierPaymentOrder order,
        IReadOnlyList<SupplierPayment> payments,
        Supplier supplier,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Para revertir pagos sueltos (anular pago, anular compra revirtiendo el pago): falla si alguno comparte
    /// orden con otros medios, porque entonces lo que se anula es la orden completa.
    /// </summary>
    Task<Result> EnsureReversibleAloneAsync(
        IReadOnlyCollection<SupplierPayment> payments,
        CompanyId companyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revierte un pago suelto (imputaciones, saldo a favor, caja, cheque). Si era el único medio de su
    /// orden, la orden queda anulada con él. Chequear antes con <see cref="EnsureReversibleAloneAsync"/>.
    /// </summary>
    Task ReversePaymentAsync(
        SupplierPayment payment,
        Supplier supplier,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);
}

public sealed class SupplierPaymentOrderService : ISupplierPaymentOrderService
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ISupplierPaymentOrderRepository _supplierPaymentOrderRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IChequeRepository _chequeRepository;

    public SupplierPaymentOrderService(
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ISupplierPaymentOrderRepository supplierPaymentOrderRepository,
        IPurchaseRepository purchaseRepository,
        IChequeRepository chequeRepository)
    {
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _supplierPaymentOrderRepository = supplierPaymentOrderRepository;
        _purchaseRepository = purchaseRepository;
        _chequeRepository = chequeRepository;
    }

    public async Task<Result<SupplierPaymentOrderOutcome>> CreateAsync(
        Supplier supplier,
        CashSession session,
        DateTime date,
        string? notes,
        IReadOnlyList<SupplierPaymentLineInput> lines,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.NoLines);

        // Primero se validan todos los medios: si uno falla, no queda nada a medio registrar.
        var resolved = new List<(PurchasePaymentMethod Method, decimal Amount, Cheque? Cheque, string? Reference)>();
        var chequeIds = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (!Enum.IsDefined(typeof(PurchasePaymentMethod), line.Method))
                return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.InvalidPaymentMethod);

            if (line.Amount <= 0)
                return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.InvalidAmount);

            var method = (PurchasePaymentMethod)line.Method;
            var amount = line.Amount;
            Cheque? cheque = null;

            // Pago con cheque: referencia un cheque en cartera y se usa por su valor completo (sin particionar).
            if (method == PurchasePaymentMethod.Check)
            {
                if (line.ChequeId is null)
                    return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.ChequeRequired);

                if (!chequeIds.Add(line.ChequeId.Value))
                    return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.DuplicateCheque);

                cheque = await _chequeRepository.GetByIdAsync(line.ChequeId.Value, companyId, cancellationToken);
                if (cheque is null)
                    return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.ChequeNotFound);

                if (cheque.Estado != ChequeStatus.EnCartera)
                    return Result<SupplierPaymentOrderOutcome>.Failure(SupplierPaymentLineErrors.ChequeNotEnCartera);

                amount = cheque.Monto;
            }

            resolved.Add((method, amount, cheque, line.Reference));
        }

        var count = await _supplierPaymentOrderRepository.CountByCompanyAsync(companyId.Value, cancellationToken);
        var order = SupplierPaymentOrder.Create(
            companyId.Value,
            supplier.Id,
            session.BranchId.Value,
            $"OP-{(count + 1).ToString().PadLeft(4, '0')}",
            date,
            notes,
            userId.Value);
        await _supplierPaymentOrderRepository.AddAsync(order, cancellationToken);

        var creditBefore = supplier.CreditBalance;
        var payments = new List<SupplierPayment>();
        var imputaciones = new List<SupplierPaymentImputacion>();

        foreach (var (method, amount, cheque, reference) in resolved)
        {
            var payment = SupplierPayment.Create(
                companyId.Value,
                supplier.Id,
                session.BranchId.Value,
                method,
                amount,
                date,
                reference,
                notes,
                userId.Value,
                cheque?.Id);
            order.AddPayment(payment);
            await _supplierPaymentRepository.AddAsync(payment, cancellationToken);
            payments.Add(payment);

            // Caja: el cheque/transferencia no mueven efectivo (dirección None); el efectivo sí.
            session.RegisterSupplierPaymentExpense(amount, payment.Id, userId, method);

            // El cheque sale de cartera (Entregado al proveedor).
            cheque?.EndorseToSupplier();

            // Imputación FIFO medio por medio, cada una con su back-link: así anular la orden revierte
            // exactamente lo que imputó cada medio. El resultado es el mismo que imputar el total.
            supplier.AddCredit(amount);
            imputaciones.AddRange(await SupplierCreditApplicator.ApplyToPendingPurchasesAsync(
                supplier, companyId.Value, _purchaseRepository, null, cancellationToken, payment.Id));
        }

        _supplierRepository.Update(supplier);

        var creditAfter = supplier.CreditBalance;
        return Result<SupplierPaymentOrderOutcome>.Success(new SupplierPaymentOrderOutcome(
            order,
            payments,
            MergeByPurchase(imputaciones),
            Math.Max(0m, creditBefore + order.TotalAmount - creditAfter),
            Math.Max(0m, creditAfter - creditBefore),
            creditAfter));
    }

    public async Task CancelOrderAsync(
        SupplierPaymentOrder order,
        IReadOnlyList<SupplierPayment> payments,
        Supplier supplier,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        foreach (var payment in payments.Where(p => p.Status == PurchasePaymentStatus.Active))
        {
            await SupplierPaymentReversal.ReverseAsync(
                payment, supplier, session, _purchaseRepository, _chequeRepository, companyId, userId, cancellationToken);
        }

        order.Cancel(userId.Value);
        _supplierRepository.Update(supplier);
    }

    public async Task<Result> EnsureReversibleAloneAsync(
        IReadOnlyCollection<SupplierPayment> payments,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        foreach (var payment in payments.Where(p => p.PaymentOrderId.HasValue))
        {
            var siblings = await _supplierPaymentRepository.ListByOrderAsync(
                payment.PaymentOrderId!.Value, companyId.Value, cancellationToken);
            if (siblings.Any(s => s.Id != payment.Id))
                return Result.Failure(SupplierPaymentLineErrors.PartOfOrder);
        }

        return Result.Success();
    }

    public async Task ReversePaymentAsync(
        SupplierPayment payment,
        Supplier supplier,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        await SupplierPaymentReversal.ReverseAsync(
            payment, supplier, session, _purchaseRepository, _chequeRepository, companyId, userId, cancellationToken);

        if (payment.PaymentOrderId is not { } orderId)
            return;

        var order = await _supplierPaymentOrderRepository.GetByIdAsync(orderId, companyId.Value, cancellationToken);
        if (order is { Status: PurchasePaymentStatus.Active })
            order.Cancel(userId.Value);
    }

    // Dos medios pueden imputar la misma factura: al usuario se le muestra una fila por factura.
    private static IReadOnlyList<SupplierPaymentImputacion> MergeByPurchase(IEnumerable<SupplierPaymentImputacion> imputaciones) =>
        imputaciones
            .GroupBy(i => i.PurchaseId)
            .Select(g => g.First() with { Amount = g.Sum(i => i.Amount) })
            .ToList();
}
