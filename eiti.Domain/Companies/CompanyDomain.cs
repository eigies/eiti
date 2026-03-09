using eiti.Domain.Primitives;

namespace eiti.Domain.Companies;

public sealed class CompanyDomain : ValueObject
{
    public string Value { get; }

    private CompanyDomain(string value)
    {
        Value = value;
    }

    public static CompanyDomain Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Company domain cannot be empty.", nameof(value));
        }

        var normalizedValue = value.Trim().ToLowerInvariant();

        if (normalizedValue.Contains('@'))
        {
            throw new ArgumentException("Company domain cannot contain '@'.", nameof(value));
        }

        if (normalizedValue.Length > 255)
        {
            throw new ArgumentException("Company domain cannot exceed 255 characters.", nameof(value));
        }

        return new CompanyDomain(normalizedValue);
    }

    public static CompanyDomain FromEmail(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            throw new ArgumentException("Email does not contain a valid domain.", nameof(email));
        }

        return Create(email[(atIndex + 1)..]);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
