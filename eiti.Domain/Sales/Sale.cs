using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Primitives;
using eiti.Domain.Transport;

namespace eiti.Domain.Sales;

public sealed class Sale : AggregateRoot<SaleId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public BranchId BranchId { get; private set; } = null!;
    public CustomerId? CustomerId { get; private set; }
    public CashSessionId? CashSessionId { get; private set; }
    public bool HasDelivery { get; private set; }
    public SaleTransportAssignmentId? TransportAssignmentId { get; private set; }
    public SaleStatus SaleStatus { get; private set; }
    public decimal NoDeliverySurchargeTotal { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsModified { get; private set; }
    public bool IsCuentaCorriente { get; private set; }
    public SaleSourceChannel? SourceChannel { get; private set; }
    public string? Code { get; private set; }
    public string? DeliveryAddress { get; private set; }
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
    public decimal CcPendingAmount => NormalizeAmount(TotalAmount - CcPaidTotal);
    public decimal PendingAmount => NormalizeAmount(TotalAmount - SettledAmount);
    public decimal ChangeAmount => NormalizeAmount(Math.Max(0m, SettledAmount - TotalAmount));

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
        DateTime createdAt,
        List<SaleDetail> details,
        string? code = null,
        string? deliveryAddress = null)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        HasDelivery = hasDelivery;
        SaleStatus = saleStatus;
        NoDeliverySurchargeTotal = noDeliverySurchargeTotal;
        CreatedAt = createdAt;
        _details = details;
        Code = code;
        DeliveryAddress = deliveryAddress;

        foreach (var detail in _details)
        {
            detail.AttachToSale(id);
        }

        TotalAmount = _details.Sum(detail => detail.TotalAmount) + noDeliverySurchargeTotal;
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
        string? deliveryAddress = null)
    {
        if (saleStatus == SaleStatus.Cancel)
        {
            throw new ArgumentException("A sale cannot be created with Cancel status.", nameof(saleStatus));
        }

        if (noDeliverySurchargeTotal < 0)
        {
            throw new ArgumentException("No-delivery surcharge total cannot be negative.", nameof(noDeliverySurchargeTotal));
        }

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
            DateTime.UtcNow,
            detailList,
            code,
            deliveryAddress);

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
        decimal noDeliverySurchargeTotal = 0,
        string? code = null)
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

        var sale = new Sale(
            SaleId.New(),
            companyId,
            branchId,
            customerId,
            hasDelivery: false,
            SaleStatus.OnHold,
            noDeliverySurchargeTotal,
            DateTime.UtcNow,
            detailList,
            code);

        sale.IsCuentaCorriente = true;
        return sale;
    }

    public SaleCcPayment AddCcPayment(SalePaymentMethod method, decimal amount, DateTime date, string? notes)
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

        if (roundedAmount > remaining)
        {
            throw new InvalidOperationException("Payment amount exceeds the remaining balance.");
        }

        var payment = SaleCcPayment.Create(Id, method, amount, date, notes);
        _ccPayments.Add(payment);

        if (NormalizeAmount(CcPaidTotal) >= NormalizeAmount(TotalAmount))
        {
            TransitionToPaidFromCc();
        }

        return payment;
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
        payment.Cancel();

        if (wasPaid && NormalizeAmount(CcPaidTotal) < NormalizeAmount(TotalAmount))
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
        string? deliveryAddress = null)
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
        DeliveryAddress = deliveryAddress;
        TotalAmount = _details.Sum(detail => detail.TotalAmount) + noDeliverySurchargeTotal;
        SetSettlement(payments, tradeIns, allowOverpayment);
        UpdatedAt = DateTime.UtcNow;
        IsModified = true;
    }

    public void MarkAsPaid(CashSessionId? cashSessionId)
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

        CashSessionId = cashAmount > 0 ? cashSessionId : null;
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
        var totalAmount = NormalizeAmount(TotalAmount);

        if (!requireAtLeastTotal && settledAmount > totalAmount)
        {
            throw new InvalidOperationException("The settled amount cannot exceed the total amount.");
        }

        if (requireAtLeastTotal && settledAmount < totalAmount)
        {
            throw new InvalidOperationException("The settled amount must cover the total amount to mark the sale as paid.");
        }
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
