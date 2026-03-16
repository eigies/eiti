using eiti.Domain.Users;
using FluentAssertions;

namespace eiti.Tests;

public sealed class UsernameValidationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghij", true)] // 50 chars
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghij1", false)] // 51 chars
    public void IsValid_ShouldReturnExpectedResult(string? value, bool expected)
    {
        Username.IsValid(value!).Should().Be(expected);
    }
}
