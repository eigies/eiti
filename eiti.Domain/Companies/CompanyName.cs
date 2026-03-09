using eiti.Domain.Primitives;

namespace eiti.Domain.Companies;

public sealed class CompanyName : ValueObject
{
    public string Value { get; }

    private CompanyName(string value)
    {
        Value = value;
    }

    public static CompanyName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Company name cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > 100)
        {
            throw new ArgumentException("Company name cannot exceed 100 characters.", nameof(value));
        }

        return new CompanyName(normalizedValue);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
