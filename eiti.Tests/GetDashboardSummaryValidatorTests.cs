using eiti.Application.Features.Dashboard.Queries.GetDashboardSummary;
using FluentAssertions;

namespace eiti.Tests;

public sealed class GetDashboardSummaryValidatorTests
{
    private readonly GetDashboardSummaryValidator _validator = new();

    [Fact]
    public void RangoValido_Pasa()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 31)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DesdePosteriorAHasta_Falla()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            new DateTime(2026, 8, 31), new DateTime(2026, 8, 1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("anterior"));
    }

    [Fact]
    public void FechaDesdeVacia_Falla()
    {
        var result = _validator.Validate(new GetDashboardSummaryQuery(
            default, new DateTime(2026, 8, 31)));

        result.IsValid.Should().BeFalse();
    }
}
