namespace eiti.Application.Common;

/// <summary>
/// Traduce fechas de negocio (un día tal como lo vive el usuario) a instantes UTC.
///
/// Todo el sistema guarda en UTC (DateTime.UtcNow, 121 usos, ningún DateTime.Now). Los filtros de
/// los reportes, en cambio, llegan como fecha suelta desde el cliente ("2026-08-14"). Compararlas
/// directo contra columnas UTC corre la ventana: en Argentina (UTC-3) el día local va de las 03:00
/// UTC a las 03:00 UTC del día siguiente, así que todo lo que pasa después de las 21:00 local cae
/// en el día siguiente y lo que pasó entre las 21:00 y las 24:00 del día anterior se cuela.
///
/// El timezone sale de la variable de entorno APP_TIMEZONE para poder cambiarlo por deploy sin
/// tocar código. Hoy hay un solo huso para todas las empresas; si alguna vez hay clientes en otro
/// país, esto pasa a ser un dato de Company y este helper se convierte en un servicio inyectado.
/// </summary>
public static class BusinessCalendar
{
    private const string DefaultTimeZoneId = "America/Argentina/Buenos_Aires";

    public static TimeZoneInfo TimeZone { get; } = ResolveTimeZone();

    /// <summary>
    /// Convierte un rango de fechas de negocio (inclusivo en ambos extremos) al rango de instantes
    /// UTC equivalente. El extremo final cubre hasta el último tick del día local.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) ToUtcRange(DateTime localFrom, DateTime localTo)
        => (StartOfDayUtc(localFrom), EndOfDayUtc(localTo));

    /// <summary>Instante UTC en que arranca ese día local (00:00:00 local).</summary>
    public static DateTime StartOfDayUtc(DateTime localDate)
        => ToUtc(localDate.Date);

    /// <summary>Instante UTC del último tick de ese día local (23:59:59.9999999 local).</summary>
    public static DateTime EndOfDayUtc(DateTime localDate)
        => ToUtc(localDate.Date.AddDays(1).AddTicks(-1));

    private static DateTime ToUtc(DateTime local)
    {
        // ConvertTimeToUtc exige Kind Unspecified o Local; los DateTime que llegan del query string
        // pueden venir con cualquier Kind, así que lo normalizamos.
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        var id = Environment.GetEnvironmentVariable("APP_TIMEZONE");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = DefaultTimeZoneId;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Si el contenedor no trae la base de husos, no podemos fallar al arrancar: Argentina no
            // usa horario de verano desde 2009, así que un offset fijo de -3 es equivalente.
            return TimeZoneInfo.CreateCustomTimeZone("EITI-AR", TimeSpan.FromHours(-3), "EITI AR", "EITI AR");
        }
    }
}
