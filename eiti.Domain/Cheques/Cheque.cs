using eiti.Domain.Companies;

namespace eiti.Domain.Cheques;

public sealed class Cheque
{
    public Guid Id { get; private set; }
    public CompanyId CompanyId { get; private set; } = null!;
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
    public ChequeOrigen Origen { get; private set; } = ChequeOrigen.Tercero;
    // Proveedor al que se le entregó un cheque propio. Null en los de terceros.
    public Guid? SupplierId { get; private set; }
    public string? Notas { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Cheque()
    {
    }

    private Cheque(
        CompanyId companyId,
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
        CompanyId = companyId;
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
        CompanyId companyId,
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
        return new Cheque(companyId, saleId, paymentMethod, null, bankId, numero, titular, cuitDni, monto, fechaEmision, fechaVencimiento, notas);
    }

    public static Cheque CreateForCcPayment(
        CompanyId companyId,
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
        return new Cheque(companyId, null, null, ccPaymentId, bankId, numero, titular, cuitDni, monto, fechaEmision, fechaVencimiento, notas);
    }

    // Cheque propio: nace entregado al proveedor, nunca pasa por cartera. El CUIT queda vacío: la empresa
    // no lo guarda (vive en su perfil fiscal) y el titular ya la identifica.
    public static Cheque CreateOwn(
        CompanyId companyId,
        Guid supplierId,
        int bankId,
        string numero,
        string titular,
        decimal monto,
        DateTime fechaEmision,
        DateTime fechaVencimiento,
        string? notas)
    {
        var cheque = new Cheque(companyId, null, null, null, bankId, numero, titular, string.Empty, monto, fechaEmision, fechaVencimiento, notas);
        cheque.Origen = ChequeOrigen.Propio;
        cheque.SupplierId = supplierId;
        cheque.Estado = ChequeStatus.Entregado;
        return cheque;
    }

    // Endoso a proveedor: el cheque sale de cartera para pagar una compra.
    // No se expone vía TransitionTo para que el cambio de estado manual no permita "Entregado".
    public void EndorseToSupplier()
    {
        if (Estado != ChequeStatus.EnCartera)
        {
            throw new InvalidOperationException("Only cheques en cartera can be endorsed to a supplier.");
        }

        Estado = ChequeStatus.Entregado;
        UpdatedAt = DateTime.UtcNow;
    }

    // Reverso del endoso (al anular el pago de la compra): vuelve a cartera.
    public void ReturnToCartera()
    {
        if (Origen == ChequeOrigen.Propio)
        {
            throw new InvalidOperationException("Own cheques were never en cartera: void them instead.");
        }

        if (Estado != ChequeStatus.Entregado)
        {
            throw new InvalidOperationException("Only cheques entregados can return to cartera.");
        }

        Estado = ChequeStatus.EnCartera;
        UpdatedAt = DateTime.UtcNow;
    }

    // Reverso de un cheque propio (al anular el pago): no vuelve a cartera, queda anulado.
    public void VoidOwn()
    {
        if (Origen != ChequeOrigen.Propio || Estado != ChequeStatus.Entregado)
        {
            throw new InvalidOperationException("Only own cheques entregados can be voided.");
        }

        Estado = ChequeStatus.Anulado;
        UpdatedAt = DateTime.UtcNow;
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
