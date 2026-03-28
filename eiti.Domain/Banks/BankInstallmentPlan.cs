namespace eiti.Domain.Banks;

public sealed class BankInstallmentPlan
{
    private static readonly int[] ValidCuotas = [1, 3, 6, 9, 12];

    public int Id { get; private set; }
    public int BankId { get; private set; }
    public int Cuotas { get; private set; }
    public decimal SurchargePct { get; private set; }
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BankInstallmentPlan()
    {
    }

    private BankInstallmentPlan(int bankId, int cuotas, decimal surchargePct)
    {
        if (!Array.Exists(ValidCuotas, c => c == cuotas))
        {
            throw new ArgumentException($"Invalid cuotas value. Valid values are: {string.Join(", ", ValidCuotas)}.", nameof(cuotas));
        }

        if (surchargePct < 0)
        {
            throw new ArgumentException("SurchargePct cannot be negative.", nameof(surchargePct));
        }

        BankId = bankId;
        Cuotas = cuotas;
        SurchargePct = surchargePct;
        Active = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static BankInstallmentPlan Create(int bankId, int cuotas, decimal surchargePct)
    {
        return new BankInstallmentPlan(bankId, cuotas, surchargePct);
    }

    public void Update(decimal surchargePct, bool active)
    {
        if (surchargePct < 0)
        {
            throw new ArgumentException("SurchargePct cannot be negative.", nameof(surchargePct));
        }

        SurchargePct = surchargePct;
        Active = active;
    }
}
