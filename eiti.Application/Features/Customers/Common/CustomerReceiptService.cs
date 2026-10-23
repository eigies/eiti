using eiti.Application.Abstractions.Repositories;
using eiti.Application.Common;
using eiti.Application.Features.Banks.Common;
using eiti.Domain.Banks;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Users;

namespace eiti.Application.Features.Customers.Common;

// Cheque recibido del cliente: entra a cartera referenciando el cobro.
public sealed record CustomerPaymentChequeInput(
    int BankId,
    string Numero,
    string Titular,
    string CuitDni,
    decimal Monto,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string? Notas);

// Un medio dentro de un cobro. Tarjeta: banco + cuotas para el recargo. Cheque: sus datos.
public sealed record CustomerPaymentLineInput(
    int Method,
    decimal Amount,
    string? Reference,
    int? CardBankId,
    int? CardCuotas,
    CustomerPaymentChequeInput? Cheque);

public sealed record CustomerReceiptOutcome(
    CustomerReceipt Receipt,
    IReadOnlyList<CustomerPayment> Payments,
    IReadOnlyList<CustomerPaymentImputacion> Imputaciones,
    decimal AppliedToSales,
    decimal CreditAdded,
    decimal CustomerCreditBalance);

/// <summary>
/// Registra un cobro de cuenta corriente con uno o más medios bajo un recibo (RC-####). Cada medio es un
/// <see cref="CustomerPayment"/> completo: recargo de tarjeta, cheque a cartera, asiento de caja e
/// imputación FIFO a las ventas CC pendientes; las ventas que quedan pagas confirman su stock una sola
/// vez. Lo usan el alta de cobro suelto (recibo de un medio) y el recibo. Valida todos los medios antes
/// de tocar nada; no llama SaveChanges.
/// </summary>
public interface ICustomerReceiptService
{
    Task<Result<CustomerReceiptOutcome>> CreateAsync(
        Customer customer,
        CashSession session,
        DateTime date,
        string? notes,
        IReadOnlyList<CustomerPaymentLineInput> lines,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);

    /// <summary>Anula el recibo entero: revierte cada medio activo y lo marca anulado.</summary>
    Task CancelReceiptAsync(
        CustomerReceipt receipt,
        IReadOnlyList<CustomerPayment> payments,
        Customer customer,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Para revertir cobros sueltos (anular cobro, anular venta revirtiendo los cobros): falla si alguno
    /// comparte recibo con otros medios, porque entonces lo que se anula es el recibo completo.
    /// </summary>
    Task<Result> EnsureReversibleAloneAsync(
        IReadOnlyCollection<CustomerPayment> payments,
        CompanyId companyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revierte un cobro suelto (imputaciones, stock, saldo a favor, caja, cheque). Si era el único medio de
    /// su recibo, el recibo queda anulado con él. Chequear antes con <see cref="EnsureReversibleAloneAsync"/>.
    /// </summary>
    Task ReversePaymentAsync(
        CustomerPayment payment,
        Customer customer,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken);
}

public sealed class CustomerReceiptService : ICustomerReceiptService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICustomerReceiptRepository _customerReceiptRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IBankRepository _bankRepository;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    public CustomerReceiptService(
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICustomerReceiptRepository customerReceiptRepository,
        ISaleRepository saleRepository,
        IBankRepository bankRepository,
        IChequeRepository chequeRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _customerRepository = customerRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _customerReceiptRepository = customerReceiptRepository;
        _saleRepository = saleRepository;
        _bankRepository = bankRepository;
        _chequeRepository = chequeRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<Result<CustomerReceiptOutcome>> CreateAsync(
        Customer customer,
        CashSession session,
        DateTime date,
        string? notes,
        IReadOnlyList<CustomerPaymentLineInput> lines,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.NoLines);

        // Primero se validan todos los medios: si uno falla, no queda nada a medio registrar.
        var resolved = new List<(CustomerPaymentLineInput Line, SalePaymentMethod Method, BankInstallmentPlan? CardPlan, int? CardBankId)>();
        foreach (var line in lines)
        {
            if (!Enum.IsDefined(typeof(SalePaymentMethod), line.Method)
                || line.Method == (int)SalePaymentMethod.CustomerCredit)
                return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.InvalidPaymentMethod);

            if (line.Amount <= 0)
                return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.InvalidAmount);

            var method = (SalePaymentMethod)line.Method;
            if (method == SalePaymentMethod.Check && line.Cheque is null)
                return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.ChequeRequired);

            // Recargo de tarjeta por plan del banco (informativo, no altera el monto imputado).
            BankInstallmentPlan? cardPlan = null;
            if (method == SalePaymentMethod.Card && line.CardBankId.HasValue)
            {
                var bank = await _bankRepository.GetByIdAsync(line.CardBankId.Value, companyId, cancellationToken);
                if (!BankUsageRules.Supports(bank, BankUsage.Card))
                    return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.CardBankInvalid);

                if (line.CardCuotas.HasValue)
                    cardPlan = CardSurchargeCalculator.FindPlan(bank, line.CardCuotas.Value);
            }

            if (method == SalePaymentMethod.Check && line.Cheque is not null)
            {
                var chequeBank = await _bankRepository.GetByIdAsync(line.Cheque.BankId, companyId, cancellationToken);
                if (!BankUsageRules.Supports(chequeBank, BankUsage.Cheque))
                    return Result<CustomerReceiptOutcome>.Failure(CustomerPaymentLineErrors.ChequeBankInvalid);
            }

            resolved.Add((line, method, cardPlan, line.CardBankId));
        }

        var count = await _customerReceiptRepository.CountByCompanyAsync(companyId.Value, cancellationToken);
        var receipt = CustomerReceipt.Create(
            companyId.Value,
            customer.Id.Value,
            session.BranchId.Value,
            $"RC-{(count + 1).ToString().PadLeft(4, '0')}",
            date,
            notes,
            userId.Value);
        await _customerReceiptRepository.AddAsync(receipt, cancellationToken);

        var creditBefore = customer.CreditBalance;
        var payments = new List<CustomerPayment>();
        var imputaciones = new List<CustomerPaymentImputacion>();
        var salesNowPaid = new List<Sale>();

        foreach (var (line, method, cardPlan, cardBankId) in resolved)
        {
            var payment = CustomerPayment.Create(
                companyId.Value,
                customer.Id.Value,
                session.BranchId.Value,
                method,
                line.Amount,
                date,
                line.Reference,
                notes,
                userId.Value);

            if (cardPlan is not null)
            {
                var surchargeAmt = CardSurchargeCalculator.Compute(line.Amount, cardPlan.SurchargePct);
                payment.SetCardData(cardBankId!.Value, cardPlan.Cuotas, cardPlan.SurchargePct, surchargeAmt);
            }

            if (line.Cheque is { } c && method == SalePaymentMethod.Check)
            {
                var cheque = Cheque.CreateForCcPayment(
                    companyId,
                    payment.Id,
                    c.BankId,
                    c.Numero,
                    c.Titular,
                    c.CuitDni,
                    c.Monto,
                    c.FechaEmision,
                    c.FechaVencimiento,
                    c.Notas);
                await _chequeRepository.AddAsync(cheque, cancellationToken);
                payment.SetChequeId(cheque.Id);
            }

            receipt.AddPayment(payment);
            await _customerPaymentRepository.AddAsync(payment, cancellationToken);
            payments.Add(payment);

            // Caja: UN asiento por cobro según método (efectivo al esperado; transfer/tarjeta visibles excluidos;
            // cheque/otros visibles con dirección None).
            session.RegisterCustomerPaymentIncome(method, line.Amount, payment.Id, userId);

            // Imputación FIFO medio por medio, cada una con su back-link: así anular el recibo revierte
            // exactamente lo que imputó cada medio. El resultado es el mismo que imputar el total.
            customer.AddCredit(line.Amount);
            var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
                customer, companyId, _saleRepository, cancellationToken, payment.Id);
            imputaciones.AddRange(application.Imputaciones);
            salesNowPaid.AddRange(application.SalesNowPaidEntities);
        }

        _customerRepository.Update(customer);

        // Confirmar stock de las ventas que pasaron a Paid. Cada venta pasa a Paid una sola vez aunque
        // la cubran varios medios. Reusa las entidades tracked del applicator (ya traen Details).
        foreach (var sale in salesNowPaid)
        {
            foreach (var detail in sale.Details)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    detail.ProductId,
                    companyId,
                    cancellationToken);
                stock.ConfirmSaleOut(detail.Quantity);
                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.SaleOut,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock confirmed as sold (CC fully paid via customer payment).",
                        userId),
                    cancellationToken);
            }
        }

        var creditAfter = customer.CreditBalance;
        return Result<CustomerReceiptOutcome>.Success(new CustomerReceiptOutcome(
            receipt,
            payments,
            MergeBySale(imputaciones),
            Math.Max(0m, creditBefore + receipt.TotalAmount - creditAfter),
            Math.Max(0m, creditAfter - creditBefore),
            creditAfter));
    }

    public async Task CancelReceiptAsync(
        CustomerReceipt receipt,
        IReadOnlyList<CustomerPayment> payments,
        Customer customer,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        foreach (var payment in payments.Where(p => p.Status == SaleCcPaymentStatus.Active))
            await ReverseAsync(payment, customer, session, companyId, userId, cancellationToken);

        receipt.Cancel(userId.Value);
    }

    public async Task<Result> EnsureReversibleAloneAsync(
        IReadOnlyCollection<CustomerPayment> payments,
        CompanyId companyId,
        CancellationToken cancellationToken)
    {
        foreach (var payment in payments.Where(p => p.ReceiptId.HasValue))
        {
            var siblings = await _customerPaymentRepository.ListByReceiptAsync(
                payment.ReceiptId!.Value, companyId.Value, cancellationToken);
            if (siblings.Any(s => s.Id != payment.Id))
                return Result.Failure(CustomerPaymentLineErrors.PartOfReceipt);
        }

        return Result.Success();
    }

    public async Task ReversePaymentAsync(
        CustomerPayment payment,
        Customer customer,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        await ReverseAsync(payment, customer, session, companyId, userId, cancellationToken);

        if (payment.ReceiptId is not { } receiptId)
            return;

        var receipt = await _customerReceiptRepository.GetByIdAsync(receiptId, companyId.Value, cancellationToken);
        if (receipt is { Status: SaleCcPaymentStatus.Active })
            receipt.Cancel(userId.Value);
    }

    private Task ReverseAsync(
        CustomerPayment payment,
        Customer customer,
        CashSession? session,
        CompanyId companyId,
        UserId userId,
        CancellationToken cancellationToken) =>
        CustomerPaymentReversal.ReverseAsync(
            payment,
            customer,
            session,
            _saleRepository,
            _branchProductStockRepository,
            _stockMovementRepository,
            _customerRepository,
            _chequeRepository,
            companyId,
            userId,
            cancellationToken);

    // Dos medios pueden imputar la misma venta: al usuario se le muestra una fila por venta.
    private static IReadOnlyList<CustomerPaymentImputacion> MergeBySale(IEnumerable<CustomerPaymentImputacion> imputaciones) =>
        imputaciones
            .GroupBy(i => i.SaleId)
            .Select(g => g.First() with { Amount = g.Sum(i => i.Amount) })
            .ToList();
}
