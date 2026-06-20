namespace eiti.Application.Common;

/// <summary>
/// Reglas de día hábil de negocio. Centraliza la comparación de fechas en hora local de Argentina
/// (UTC-3, sin horario de verano) que antes estaba duplicada en varios handlers de caja/ventas.
/// </summary>
public static class BusinessDay
{
    // Argentina es UTC-3 sin DST — se comparan fechas de calendario en hora local de negocio.
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    /// <summary>True si <paramref name="openedAtUtc"/> corresponde a un día de calendario anterior al de hoy (hora AR).</summary>
    public static bool IsFromPreviousBusinessDay(DateTime openedAtUtc)
    {
        var openedLocal = openedAtUtc + ArgentinaOffset;
        var nowLocal = DateTime.UtcNow + ArgentinaOffset;
        return openedLocal.Date < nowLocal.Date;
    }
}
