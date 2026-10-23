using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Branches;

public sealed record FiscalPointOfSaleId(Guid Value)
{
    public static FiscalPointOfSaleId New() => new(Guid.NewGuid());
}

/// <summary>
/// Punto de venta de ARCA (tipo "Web Services") de una sucursal. Relación 1:1: cada sucursal factura
/// en el suyo y ningún otro la comparte. El número lo da de alta el contador del cliente en ARCA.
/// </summary>
public sealed class FiscalPointOfSale : Entity<FiscalPointOfSaleId>
{
    public const int MaxNumber = 99999;

    public CompanyId CompanyId { get; private set; } = null!;
    public int Number { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private FiscalPointOfSale()
    {
    }

    private FiscalPointOfSale(FiscalPointOfSaleId id, CompanyId companyId, int number, DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        Number = ValidateNumber(number);
        CreatedAt = createdAt;
    }

    public static FiscalPointOfSale Create(CompanyId companyId, int number) =>
        new(FiscalPointOfSaleId.New(), companyId, number, DateTime.UtcNow);

    public void ChangeNumber(int number)
    {
        Number = ValidateNumber(number);
        UpdatedAt = DateTime.UtcNow;
    }

    private static int ValidateNumber(int number)
    {
        if (number < 1 || number > MaxNumber)
        {
            throw new ArgumentException($"The point of sale must be between 1 and {MaxNumber}.", nameof(number));
        }

        return number;
    }
}
