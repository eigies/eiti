using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class Username : ValueObject
{
    public string Value { get; }

    private Username(string value)
    {
        Value = value;
    }

    public static Username Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Username cannot be empty.", nameof(value));
        }

        if (value.Length < 3)
        {
            throw new ArgumentException("Username must be at least 3 characters.", nameof(value));
        }

        if (value.Length > 50)
        {
            throw new ArgumentException("Username cannot exceed 50 characters.", nameof(value));
        }

        return new Username(value.ToLowerInvariant());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
