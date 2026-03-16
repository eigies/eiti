using eiti.Domain.Primitives;
using System.Text.RegularExpressions;

namespace eiti.Domain.Customers;

public sealed class Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El email no puede estar vacío.", nameof(value));
        }

        if (!EmailRegex.IsMatch(value))
        {
            throw new ArgumentException("El email no tiene un formato válido.", nameof(value));
        }

        return new Email(value.ToLowerInvariant());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value);

    public override string ToString() => Value;
}
