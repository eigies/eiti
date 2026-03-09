using eiti.Domain.Primitives;

namespace eiti.Domain.Customers;

public sealed class CustomerName : ValueObject
{
    public string Value { get; }

    private CustomerName(string value)
    {
        Value = value;
    }

    public static CustomerName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(value));
        }

        if (value.Length > 100)
        {
            throw new ArgumentException("El nombre no puede exceder 100 caracteres.", nameof(value));
        }

        return new CustomerName(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
