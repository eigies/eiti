namespace eiti.Domain.Cheques;

public sealed class Cheque
{
    public Guid Id { get; private set; }
    public Guid? SalePaymentSaleId { get; private set; }
    public int? SalePaymentMethod { get; private set; }
    public Guid? SaleCcPaymentId { get; private set; }
    public int BankId { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string Titular { get; private set; } = string.Empty;
    public string CuitDni { get; private set; } = string.Empty;
    public decimal Monto { get; private set; }
    public DateTime FechaEmision { get; private set; }
    public DateTime FechaVencimiento { get; private set; }
    public ChequeStatus Estado { get; private set; }
    public string? Notas { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Cheque()
    {
    }

    private Cheque(
        Guid? salePaymentSaleId,
        int? salePaymentMethod,
        Guid? saleCcPaymentId,
        int bankId,
        string numero,
        string titular,
        string cuitDni,
        decimal monto,
        DateTime fechaEmision,
        DateTime fechaVencimiento,
        string? notas)
    {
        Id = Guid.NewGuid();
        SalePaymentSaleId = salePaymentSaleId;
        SalePaymentMethod = salePaymentMethod;
        SaleCcPaymentId = saleCcPaymentId;
        BankId = bankId;
        Numero = numero.Trim();
        Titular = titular.Trim();
        CuitDni = cuitDni.Trim();
        Monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero);
        FechaEmision = fechaEmision.Date;
        FechaVencimiento = fechaVencimiento.Date;
        Estado = ChequeStatus.EnCartera;
        Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Cheque CreateForRegularSale(
        Guid saleId,
        int paymentMethod,
        int bankId,
        string numero,
        string titular,
        string cuitDni,
        decimal monto,
        DateTime fechaEmision,
        DateTime fechaVencimiento,
        string? notas)
    {
        return new Cheque(saleId, paymentMethod, null, bankId, numero, titular, cuitDni, monto, fechaEmision, fechaVencimiento, notas);
    }

    public static Cheque CreateForCcPayment(
        Guid ccPaymentId,
        int bankId,
        string numero,
        string titular,
        string cuitDni,
        decimal monto,
        DateTime fechaEmision,
        DateTime fechaVencimiento,
        string? notas)
    {
        return new Cheque(null, null, ccPaymentId, bankId, numero, titular, cuitDni, monto, fechaEmision, fechaVencimiento, notas);
    }

    public void TransitionTo(ChequeStatus newStatus)
    {
        if (Estado == newStatus)
        {
            throw new InvalidOperationException($"Cannot transition from {Estado} to {newStatus}.");
        }

        var allowed = Estado switch
        {
            ChequeStatus.EnCartera => new[] { ChequeStatus.Depositado, ChequeStatus.Anulado },
            ChequeStatus.Depositado => new[] { ChequeStatus.Acreditado, ChequeStatus.Rechazado, ChequeStatus.Anulado },
            ChequeStatus.Acreditado => Array.Empty<ChequeStatus>(),
            ChequeStatus.Rechazado => Array.Empty<ChequeStatus>(),
            ChequeStatus.Anulado => Array.Empty<ChequeStatus>(),
            _ => Array.Empty<ChequeStatus>()
        };

        if (!Array.Exists(allowed, s => s == newStatus))
        {
            throw new InvalidOperationException($"Cannot transition from {Estado} to {newStatus}.");
        }

        Estado = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
