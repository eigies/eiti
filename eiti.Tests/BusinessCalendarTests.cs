using eiti.Application.Common;
using FluentAssertions;

namespace eiti.Tests;

public sealed class BusinessCalendarTests
{
    // Argentina: UTC-3 todo el año (sin horario de verano desde 2009).
    private const int OffsetHours = 3;

    [Fact]
    public void StartOfDayUtc_DevuelveLasTresDeLaMañanaUtc()
    {
        var utc = BusinessCalendar.StartOfDayUtc(new DateTime(2026, 8, 14));

        utc.Should().Be(new DateTime(2026, 8, 14, OffsetHours, 0, 0));
    }

    [Fact]
    public void EndOfDayUtc_CruzaAlDiaSiguienteEnUtc()
    {
        var utc = BusinessCalendar.EndOfDayUtc(new DateTime(2026, 8, 14));

        // 23:59:59.9999999 local del 14 = 02:59:59.9999999 UTC del 15.
        utc.Date.Should().Be(new DateTime(2026, 8, 15));
        utc.Hour.Should().Be(OffsetHours - 1);
        utc.Minute.Should().Be(59);
    }

    // Este es el bug que motivó el helper: una venta de las 21:13 local quedaba grabada como
    // 00:13 UTC del día siguiente y se caía del rango del día en que realmente ocurrió.
    [Theory]
    [InlineData(2026, 8, 15, 0, 13)]   // 21:13 local del 14
    [InlineData(2026, 8, 15, 0, 4)]    // 21:04 local del 14
    [InlineData(2026, 8, 15, 2, 59)]   // 23:59 local del 14
    [InlineData(2026, 8, 14, 3, 0)]    // 00:00 local del 14
    [InlineData(2026, 8, 14, 14, 30)]  // 11:30 local del 14
    public void ToUtcRange_IncluyeTodoLoQueOcurrioEseDiaLocal(int y, int m, int d, int h, int min)
    {
        var (from, to) = BusinessCalendar.ToUtcRange(new DateTime(2026, 8, 14), new DateTime(2026, 8, 14));
        var instanteUtc = new DateTime(y, m, d, h, min, 0);

        instanteUtc.Should().BeOnOrAfter(from);
        instanteUtc.Should().BeOnOrBefore(to);
    }

    [Theory]
    [InlineData(2026, 8, 14, 2, 59)]   // 23:59 local del 13 -> NO es del 14
    [InlineData(2026, 8, 15, 3, 0)]    // 00:00 local del 15 -> NO es del 14
    public void ToUtcRange_ExcluyeLoQueNoOcurrioEseDiaLocal(int y, int m, int d, int h, int min)
    {
        var (from, to) = BusinessCalendar.ToUtcRange(new DateTime(2026, 8, 14), new DateTime(2026, 8, 14));
        var instanteUtc = new DateTime(y, m, d, h, min, 0);

        (instanteUtc >= from && instanteUtc <= to).Should().BeFalse();
    }

    [Fact]
    public void ToUtcRange_RangoDeVariosDias_CubreExactamenteEsosDias()
    {
        var (from, to) = BusinessCalendar.ToUtcRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        from.Should().Be(new DateTime(2026, 8, 1, OffsetHours, 0, 0));
        to.Should().BeCloseTo(new DateTime(2026, 9, 1, OffsetHours, 0, 0), TimeSpan.FromSeconds(1));
        (to - from).TotalDays.Should().BeApproximately(31, 0.001);
    }

    [Fact]
    public void ToUtcRange_IgnoraLaHoraQueVengaEnElRequest()
    {
        var conHora = BusinessCalendar.ToUtcRange(
            new DateTime(2026, 8, 14, 17, 45, 0),
            new DateTime(2026, 8, 14, 9, 12, 0));
        var sinHora = BusinessCalendar.ToUtcRange(new DateTime(2026, 8, 14), new DateTime(2026, 8, 14));

        conHora.Should().Be(sinHora);
    }

    [Fact]
    public void TimeZone_ResuelveAMenosTres()
    {
        BusinessCalendar.TimeZone.GetUtcOffset(new DateTime(2026, 8, 14))
            .Should().Be(TimeSpan.FromHours(-OffsetHours));
    }
}
