using eiti.Domain.Customers;
using FluentAssertions;

namespace eiti.Tests;

public sealed class EmailValidationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("notanemail", false)]
    [InlineData("user@domain.com", true)]
    [InlineData("user@", false)]
    public void IsValid_ShouldReturnExpectedResult(string? value, bool expected)
    {
        Email.IsValid(value!).Should().Be(expected);
    }
}
