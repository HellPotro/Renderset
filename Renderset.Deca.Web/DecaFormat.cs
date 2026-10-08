using System.Globalization;

namespace Renderset.Deca.Web;

/// <summary>
/// Fechas en hora de España, que es la del documento, aunque el servidor
/// esté en UTC.
/// </summary>
public static class DecaFormat
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private static readonly TimeZoneInfo Zone = new DecaOptions().Zone;

    public static DateTime ToLocal(
        DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Zone);

    public static string LocalDateTime(
        DateTime? utc) =>
        utc is null
            ? "—"
            : ToLocal(utc.Value).ToString("dd/MM/yyyy HH:mm", Spanish);

    public static string Date(
        DateOnly? date) =>
        date is null
            ? "—"
            : date.Value.ToString("dd/MM/yyyy", Spanish);

    /// <summary>
    /// Ahora en España, con su zona.
    /// </summary>
    public static DateTimeOffset NowLocal =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);

    /// <summary>
    /// Hoy en España.
    /// </summary>
    public static DateOnly Today =>
        DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));

    public static string Size(
        long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024.0 / 1024.0:0.0} MB"
            : $"{Math.Max(1, bytes / 1024)} KB";
}
