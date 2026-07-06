using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Primitives;
using eiti.Domain.Transport;
using eiti.Domain.Users;

namespace eiti.Domain.Sales;

public sealed class Sale : AggregateRoot<SaleId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public CustomerId? CustomerId { get; private set; }
    public CashDrawerId? CashDrawerId { get; private set; }
    public CashSessionId? CashSessionId { get; private set; }
    public bool HasDelivery { get; private set; }
    public SaleTransportAssignmentId? TransportAssignmentId { get; private set; }
    public SaleStatus SaleStatus { get; private set; }
    public decimal NoDeliverySurchargeTotal { get; private set; }
    public decimal CardSurchargeTotal { get; private set; }
    public decimal GeneralDiscountPercent { get; private set; }
    public decimal OriginalTotal { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal? ManualOverridePrice { get; private set; }
    public Guid? OverriddenByUserId { get; private set; }
    public DateTime? OverriddenAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsModified { get; private set; }
    public bool IsCuentaCorriente { get; private set; }
    public SaleSourceChannel? SourceChannel { get; private set; }
    public string? Code { get; private set; }
    public string? DeliveryAddress { get; private set; }
    public string? ContactPhone { get; private set; }
    private readonly List<SaleDetail> _details = [];
    private readonly List<SalePayment> _payments = [];
    private readonly List<SaleTradeIn> _tradeIns = [];
    private readonly List<SaleCcPayment> _ccPayments = [];
    public IReadOnlyCollection<SaleDetail> Details => _details;
    public IReadOnlyCollection<SalePayment> Payments => _payments;
    public IReadOnlyCollection<SaleTradeIn> TradeIns => _tradeIns;
    public IReadOnlyCollection<SaleCcPayment> CcPayments => _ccPayments;
    public decimal MonetaryPaidAmount => _payments.Sum(payment => payment.Amount);
    public decimal TradeInAmount => _tradeIns.Sum(tradeIn => tradeIn.Amount);
    public decimal SettledAmount => MonetaryPaidAmount + TradeInAmount;
    public decimal CcPaidTotal => _ccPayments
        .Where(p => p.Status == SaleCcPaymentStatus.Active)
        .Sum(p => p.Amount);
    // En CC el canje (TradeInAmount) salda parte de la deuda al igual que en la venta normal:
    // lo saldado es lo cobrado a cuenta + el valor del canje entregado al momento de la venta.
    public decimal CcSettledTotal => CcPaidTotal + TradeInAmount;
    public decimal CcPendingAmount => NormalizeAmount(TotalAmount - CcSettledTotal);
    public decimal EffectiveTotal => TotalAmount + CardSurchargeTotal;
    public decimal PendingAmount => NormalizeAmount(EffectiveTotal - SettledAmount);
    public decimal ChangeAmount => NormalizeAmount(Math.Max(0m, SettledAmount - EffectiveTotal));

    private Sale()
    {
    }

    private Sale(
        SaleId id,
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        bool hasDelivery,
        SaleStatus saleStatus,
        decimal noDeliverySurchargeTotal,
        decimal cardSurchargeTotal,
        decimal generalDiscountPercent,
        DateTime createdAt,
        List<SaleDetail> details,
        string? code = null,
        string? deliveryAddress = null,
        string? contactPhone = null)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        HasDelivery = hasDelivery;
        SaleStatus = saleStatus;
        NoDeliverySurchargeTotal = noDeliverySurchargeTotal;
        CardSurchargeTotal = cardSurchargeTotal;
        GeneralDiscountPercent = NormalizeAmount(generalDiscountPercent);
        CreatedAt = createdAt;
        _details = details;
        Code = code;
        DeliveryAddress = deliveryAddress;
        ContactPhone = contactPhone;

        foreach (var detail in _details)
        {
            detail.AttachToSale(id);
        }

        RecalculateTotal();
    }

    public static Sale Create(
        CompanyId companyId,
        BranchId branchId,
        CustomerId? customerId,
        bool hasDelivery,
        SaleStatus saleStatus,
        IEnumerable<SaleDetail> details,
        IEnumerable<SalePayment>? payments = null,
        IEnumerable<SaleTradeIn>? tradeIns = null,
        bool allowOverpayment = false,
        decimal noDeliverySurchargeTotal = 0,
        string? code = null,
        string? deliveryAddress = null,
        decimal generalDiscountPercent = 0,
        decimal cardSurchargeTotal = 0,
        string? contactPhone = null)
    {
        if (saleStatus == SaleStatus.Cancel)
        {
            throw new ArgumentException("A sale cannot be created with Cancel status.", nameof(saleStatus));
        }

        if (noDeliverySurchargeTotal < 0)
        {
            throw new ArgumentException("No-delivery surcharge total cannot be negative.", nameof(noDeliverySurchargeTotal));
        }

        if (cardSurchargeTotal < 0)
        {
            throw new ArgumentException("Card surcharge total cannot be negative.", nameof(cardSurchargeTotal));
        }

        ValidateDiscountPercent(generalDiscountPercent);

        var detailList = details.ToList();

        if (detailList.Count == 0)
        {
            throw new ArgumentException("A sale must contain at least one detail.", nameof(details));
        }

        var sale = new Sale(
            SaleId.New(),
            companyId,
            branchId,
            customerId,
            hasDelivery,
            saleStatus,
            noDeliverySurchargeTotal,
            cardSurchargeTotal,
            generalDiscountPercent,
            DateTime.UtcNow,
            detailList,
            code,
            deliveryAddress,
            contactPhone);

        sale.SetSettlement(
            payments,
            tradeIns,
            allowOverpayment || saleStatus == SaleStatus.Paid);

        return sale;
    }

    public static Sale CreateCc(
        CompanyId companyId,
        BranchId branchId,
        CustomerId customerId,
        IEnumerable<SaleDetail> details,
        IEnumerable<SaleTradeIn>? tradeIns = null,
        decimal noDeliverySurchargeTotal = 0,
        string? code = null,
        decimal generalDiscountPercent = 0,
        decimal? manualOverridePrice = null,
        Guid? overriddenByUserId = null)
    {
        var detailList = details.ToList();

        if (detailList.Count == 0)
        {
            throw new ArgumentException("A sale must contain at least one detail.", nameof(details));
        }

        if (noDeliverySurchargeTotal < 0)
        {
            throw new ArgumentException("No-delivery surcharge total cannot be negative.", nameof(noDeliverySurchargeTotal));
        }

        ValidateDiscountPercent(generalDiscountPercent);

        var sale = new Sale(
            SaleId.New(),
            companyId,
            branchId,
            customerId,
            hasDelivery: false,
            SaleStatus.OnHold,
            noDeliverySurchargeTotal,
            cardSurchargeTotal: 0,
            generalDiscountPercent,
            DateTime.UtcNow,
            detailList,
            code);

        sale.IsCuentaCorriente = true;

        if (manualOverridePrice.HasValue)
        {
            sale.SetManualOverride(manualOverridePrice.Value, overriddenByUserId);
        }

        // El canje se adjunta después de fijar el total (incluido override) para validar contra él.
        sale.AttachCcTradeIns(tradeIns);

        return sale;
    }

    // Adjunta los canjes de una venta CC. El valor del canje salda parte de la deuda (vía CcSettledTotal),
    // reduciendo CcPendingAmount, pero NO cambia el estado por sí mismo: la venta queda OnHold. El paso a
    // Pagada ocurre solo con pagos CC reales (que ya contemplan el canje al comparar contra CcSettledTotal).
    // Así, si el canje cubre el total, la venta queda OnHold con saldo 0 y la anulación libera la reserva
    // correctamente (las ventas CC mantienen el stock reservado; nunca hacen ConfirmSaleOut).
    // El ingreso de stock (TradeInIn) lo registra el handler.
    private void AttachCcTradeIns(IEnumerable<SaleTradeIn>? tradeIns)
    {
        var tradeInList = (tradeIns ?? []).ToList();
        if (tradeInList.Count == 0)
        {
            return;
        }

        if (tradeInList.GroupBy(tradeIn => tradeIn.ProductId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Duplicated trade-in products are not allowed.", nameof(tradeIns));
        }

        foreach (var tradeIn in tradeInList)
        {
            tradeIn.AttachToSale(Id);
            _tradeIns.Add(tradeIn);
        }

        if (NormalizeAmount(TradeInAmount) > NormalizeAmount(TotalAmount))
        {
            throw new InvalidOperationException("El canje no puede superar el total de la venta.");
        }
    }

    public void SetManualOverride(decimal price, Guid? userId)
    {
        if (price < 0)
        {
            throw new ArgumentException("Manual override price cannot be negative.", nameof(price));
        }

        ManualOverridePrice = NormalizeAmount(price);
        OverriddenByUserId = userId;
        OverriddenAt = DateTime.UtcNow;
        TotalAmount = ManualOverridePrice.Value;
    }

    public void ClearManualOverride()
    {
        ManualOverridePrice = null;
        OverriddenByUserId = null;
        OverriddenAt = null;
        RecalculateTotal();
    }

    public SaleCcPayment AddCcPayment(SalePaymentMethod method, decimal amount, DateTime date, string? notes, bool allowOverpayment = false)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be added to Cuenta Corriente sales.");
        }

        if (SaleStatus == SaleStatus.Cancel)
        {
            throw new InvalidOperationException("Cannot add payments to a cancelled sale.");
        }

        var remaining = NormalizeAmount(CcPendingAmount);
        var roundedAmount = NormalizeAmount(amount);

        if (!allowOverpayment && roundedAmount > remaining)
        {
            throw new InvalidOperationException("Payment amount exceeds the remaining balance.");
        }

        var payment = SaleCcPayment.Create(Id, method, amount, date, notes);
        _ccPayments.Add(payment);

        if (NormalizeAmount(CcSettledTotal) >= NormalizeAmount(TotalAmount))
        {
            TransitionToPaidFromCc();
        }

        return payment;
    }

    // Imputación interna FIFO (bolsa por cliente): aplica una porción del saldo a favor del cliente a esta
    // venta CC pendiente, registrando un SaleCcPayment con método CustomerCredit y back-link al CustomerPayment
    // que generó el crédito. NO toca la caja (el efectivo ya se movió en el cobro real). Devuelve true si la
    // venta pasó a Paid con esta imputación (para que el handler confirme el stock).
    public bool ApplyCustomerCredit(decimal amount, DateTime date, Guid customerPaymentId, string? notes)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be added to Cuenta Corriente sales.");
        }

        if (SaleStatus == SaleStatus.Cancel)
        {
            throw new InvalidOperationException("Cannot add payments to a cancelled sale.");
        }

        var wasPaid = SaleStatus == SaleStatus.Paid;

        var payment = SaleCcPayment.Create(
            Id,
            SalePaymentMethod.CustomerCredit,
            amount,
            date,
            notes,
            groupId: null,
            customerPaymentId: customerPaymentId);
        _ccPayments.Add(payment);

        if (!wasPaid && NormalizeAmount(CcSettledTotal) >= NormalizeAmount(TotalAmount))
        {
            TransitionToPaidFromCc();
            return true;
        }

        return false;
    }

    // Reversa de las imputaciones internas (CustomerCredit) generadas por un cobro a nivel cliente que se anula.
    // Cancela las filas activas con ese customerPaymentId; si la venta estaba Paid y queda pendiente, vuelve a
    // OnHold. Devuelve true si revirtió desde Paid (para que el handler revierta el stock).
    public bool RevertCustomerCredit(Guid customerPaymentId)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be cancelled on Cuenta Corriente sales.");
        }

        var rows = _ccPayments
            .Where(p => p.CustomerPaymentId == customerPaymentId && p.Status == SaleCcPaymentStatus.Active)
            .ToList();

        if (rows.Count == 0)
        {
            return false;
        }

        var wasPaid = SaleStatus == SaleStatus.Paid;

        foreach (var row in rows)
        {
            row.Cancel();
        }

        if (wasPaid && NormalizeAmount(CcSettledTotal) < NormalizeAmount(TotalAmount))
        {
            RevertToOnHoldFromCc();
            return true;
        }

        return false;
    }

    public IReadOnlyList<SaleCcPayment> AddCcPaymentGroup(
        IEnumerable<(SalePaymentMethod Method, decimal Amount)> methods,
        DateTime date,
        string? notes,
        bool allowOverpayment = false)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be added to Cuenta Corriente sales.");
        }

        if (SaleStatus == SaleStatus.Cancel)
        {
            throw new InvalidOperationException("Cannot add payments to a cancelled sale.");
        }

        var methodList = methods.ToList();
        if (methodList.Count == 0)
        {
            throw new ArgumentException("At least one payment method is required.", nameof(methods));
        }

        var totalPayment = methodList.Sum(m => NormalizeAmount(m.Amount));
        var remaining = NormalizeAmount(CcPendingAmount);

        if (!allowOverpayment && totalPayment > remaining)
        {
            throw new InvalidOperationException("Payment amount exceeds the remaining balance.");
        }

        var groupId = Guid.NewGuid();
        var created = new List<SaleCcPayment>();

        foreach (var (method, amount) in methodList)
        {
            if (NormalizeAmount(amount) <= 0) continue;

            var payment = SaleCcPayment.Create(Id, method, amount, date, notes, groupId);
            _ccPayments.Add(payment);
            created.Add(payment);
        }

        if (NormalizeAmount(CcSettledTotal) >= NormalizeAmount(TotalAmount))
        {
            TransitionToPaidFromCc();
        }

        return created;
    }

    public decimal CancelCcPaymentGroup(Guid groupId)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be cancelled on Cuenta Corriente sales.");
        }

        var groupPayments = _ccPayments
            .Where(p => p.GroupId == groupId && p.Status == SaleCcPaymentStatus.Active)
            .ToList();

        if (groupPayments.Count == 0)
        {
            throw new InvalidOperationException("No active payments found for this group.");
        }

        var wasPaid = SaleStatus == SaleStatus.Paid;
        var cancelledAmount = 0m;

        foreach (var payment in groupPayments)
        {
            cancelledAmount += payment.Amount;
            payment.Cancel();
        }

        if (wasPaid && NormalizeAmount(CcSettledTotal) < NormalizeAmount(TotalAmount))
        {
            RevertToOnHoldFromCc();
        }

        return cancelledAmount;
    }

    public void CancelCcPayment(SaleCcPaymentId paymentId)
    {
        if (!IsCuentaCorriente)
        {
            throw new InvalidOperationException("CC payments can only be cancelled on Cuenta Corriente sales.");
        }

        var payment = _ccPayments.FirstOrDefault(p => p.Id == paymentId);

        if (payment is null)
        {
            throw new InvalidOperationException("Payment not found.");
        }

        var wasPaid = SaleStatus == SaleStatus.Paid;

        if (payment.GroupId.HasValue)
        {
            var groupPayments = _ccPayments
                .Where(p => p.GroupId == payment.GroupId && p.Status == SaleCcPaymentStatus.Active)
                .ToList();

            foreach (var gp in groupPayments)
            {
                gp.Cancel();
            }
        }
        else
        {
            payment.Cancel();
        }

        if (wasPaid && NormalizeAmount(CcSettledTotal) < NormalizeAmount(TotalAmount))
        {
            RevertToOnHoldFromCc();
        }
    }

    private void TransitionToPaidFromCc()
    {
        SaleStatus = SaleStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    private void RevertToOnHoldFromCc()
    {
        SaleStatus = SaleStatus.OnHold;
        PaidAt = null;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void Update(
        CustomerId? customerId,
        SaleStatus saleStatus,
        bool hasDelivery,
        IEnumerable<SaleDetail> details,
        IEnumerable<SalePayment>? payments = null,
        IEnumerable<SaleTradeIn>? tradeIns = null,
        bool allowOverpayment = false,
        decimal noDeliverySurchargeTotal = 0,
        string? deliveryAddress = null,
        decimal generalDiscountPercent = 0,
        decimal cardSurchargeTotal = 0,
        string? contactPhone = null)
    {
        if (SaleStatus != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only sales in OnHold status can be modified.");
        }

        var detailList = details.ToList();

        if (detailList.Count == 0)
        {
            throw new ArgumentException("A sale must contain at least one detail.", nameof(details));
        }

        if (saleStatus == SaleStatus.Paid)
        {
            throw new InvalidOperationException("A sale cannot be marked as paid through the generic update operation.");
        }

        if (noDeliverySurchargeTotal < 0)
        {
            throw new ArgumentException("No-delivery surcharge total cannot be negative.", nameof(noDeliverySurchargeTotal));
        }

        ValidateDiscountPercent(generalDiscountPercent);

        if (!hasDelivery)
        {
            TransportAssignmentId = null;
        }

        _details.Clear();

        foreach (var detail in detailList)
        {
            detail.AttachToSale(Id);
            _details.Add(detail);
        }

        CustomerId = customerId;
        HasDelivery = hasDelivery;
        SaleStatus = saleStatus;
        NoDeliverySurchargeTotal = noDeliverySurchargeTotal;
        CardSurchargeTotal = cardSurchargeTotal;
        GeneralDiscountPercent = NormalizeAmount(generalDiscountPercent);
        DeliveryAddress = deliveryAddress;
        ContactPhone = contactPhone;
        RecalculateTotal();
        SetSettlement(payments, tradeIns, allowOverpayment);
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void SetCashDrawer(CashDrawerId? cashDrawerId)
    {
        CashDrawerId = cashDrawerId;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void MarkAsPaid(CashDrawerId? cashDrawerId, CashSessionId? cashSessionId)
    {
        if (SaleStatus == SaleStatus.Paid)
        {
            throw new InvalidOperationException("The sale is already paid.");
        }

        if (SaleStatus != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only sales in OnHold status can be marked as paid.");
        }

        ValidateSettlement(requireAtLeastTotal: true);

        var cashAmount = GetPaymentAmount(SalePaymentMethod.Cash);
        if (cashAmount > 0 && cashSessionId is null)
        {
            throw new InvalidOperationException("A cash session is required when cash payment amount is greater than zero.");
        }

        var transferAmount = GetPaymentAmount(SalePaymentMethod.Transfer);
        if (CashDrawerId is not null && cashDrawerId is not null && CashDrawerId != cashDrawerId)
        {
            throw new InvalidOperationException("The sale must be paid using the originally assigned cash drawer.");
        }

        CashDrawerId = cashDrawerId ?? CashDrawerId;
        var cardAmount = GetPaymentAmount(SalePaymentMethod.Card);
        var checkAmount = GetPaymentAmount(SalePaymentMethod.Check);
        CashSessionId = (cashAmount > 0 || transferAmount > 0 || cardAmount > 0 || checkAmount > 0) ? cashSessionId : null;
        SaleStatus = SaleStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public decimal GetPaymentAmount(SalePaymentMethod method)
    {
        return _payments
            .Where(payment => payment.Method == method)
            .Sum(payment => payment.Amount);
    }

    public void Cancel()
    {
        if (SaleStatus == SaleStatus.Cancel)
        {
            throw new InvalidOperationException("The sale is already cancelled.");
        }

        SaleStatus = SaleStatus.Cancel;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void AssignTransport(SaleTransportAssignmentId transportAssignmentId)
    {
        if (!HasDelivery)
        {
            throw new InvalidOperationException("This sale is not marked for delivery.");
        }

        TransportAssignmentId = transportAssignmentId;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void ClearTransportAssignment()
    {
        TransportAssignmentId = null;
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void SetSourceChannel(SaleSourceChannel? channel) => SourceChannel = channel;

    public void SetDeliveryAddress(string? address) => DeliveryAddress = address;

    public void SetContactPhone(string? phone) => ContactPhone = phone;

    private void RecalculateTotal()
    {
        var subtotal = _details.Sum(detail => detail.TotalAmount) + NoDeliverySurchargeTotal;
        OriginalTotal = NormalizeAmount(subtotal);

        if (GeneralDiscountPercent > 0)
        {
            subtotal = subtotal * (1m - GeneralDiscountPercent / 100m);
        }

        TotalAmount = ManualOverridePrice ?? NormalizeAmount(subtotal);
    }

    private void SetSettlement(
        IEnumerable<SalePayment>? payments,
        IEnumerable<SaleTradeIn>? tradeIns,
        bool requireAtLeastTotal)
    {
        var paymentList = (payments ?? []).ToList();
        var tradeInList = (tradeIns ?? []).ToList();

        if (paymentList.Any(payment => !Enum.IsDefined(typeof(SalePaymentMethod), payment.Method)))
        {
            throw new ArgumentException("The selected payment method is invalid.", nameof(payments));
        }

        if (paymentList.GroupBy(payment => payment.Method).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Duplicated payment methods are not allowed.", nameof(payments));
        }

        if (tradeInList.GroupBy(tradeIn => tradeIn.ProductId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Duplicated trade-in products are not allowed.", nameof(tradeIns));
        }

        _payments.Clear();
        _tradeIns.Clear();

        foreach (var payment in paymentList)
        {
            payment.AttachToSale(Id);
            _payments.Add(payment);
        }

        foreach (var tradeIn in tradeInList)
        {
            tradeIn.AttachToSale(Id);
            _tradeIns.Add(tradeIn);
        }

        ValidateSettlement(requireAtLeastTotal);
    }

    private void ValidateSettlement(bool requireAtLeastTotal)
    {
        var settledAmount = NormalizeAmount(SettledAmount);
        var effectiveTotal = NormalizeAmount(TotalAmount + CardSurchargeTotal);

        if (!requireAtLeastTotal && settledAmount > effectiveTotal)
        {
            throw new InvalidOperationException("The settled amount cannot exceed the total amount.");
        }

        if (requireAtLeastTotal && settledAmount < effectiveTotal)
        {
            throw new InvalidOperationException("The settled amount must cover the total amount to mark the sale as paid.");
        }
    }

    private static void ValidateDiscountPercent(decimal value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentException("General discount percent must be between 0 and 100.");
        }
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
