namespace eiti.Domain.Banks;

public sealed class Bank
{
    private static readonly int[] ValidCuotas = [1, 3, 6, 9, 12];

    private readonly List<BankInstallmentPlan> _installmentPlans = [];

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<BankInstallmentPlan> InstallmentPlans => _installmentPlans.AsReadOnly();

    private Bank()
    {
    }

    private Bank(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Bank name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        Active = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Bank Create(string name)
    {
        return new Bank(name);
    }

    public void Update(string name, bool active)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Bank name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        Active = active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpsertInstallmentPlan(int cuotas, decimal surchargePct, bool active)
    {
        if (!Array.Exists(ValidCuotas, c => c == cuotas))
        {
            throw new ArgumentException($"Invalid cuotas value. Valid values are: {string.Join(", ", ValidCuotas)}.", nameof(cuotas));
        }

        if (surchargePct < 0)
        {
            throw new ArgumentException("SurchargePct cannot be negative.", nameof(surchargePct));
        }

        var existing = _installmentPlans.Find(p => p.Cuotas == cuotas);

        if (existing is null)
        {
            var plan = BankInstallmentPlan.Create(Id, cuotas, surchargePct);
            _installmentPlans.Add(plan);
        }
        else
        {
            existing.Update(surchargePct, active);
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
