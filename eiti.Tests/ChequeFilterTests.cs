using eiti.Application.Abstractions.Repositories;
using FluentAssertions;

namespace eiti.Tests;

public sealed class ChequeFilterTests
{
    [Fact]
    public void ChequeFilters_ShouldCarryNumeroFilter()
    {
        var filters = new ChequeFilters(null, null, null, null, "123");

        filters.Numero.Should().Be("123");
    }
}
