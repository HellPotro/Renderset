namespace Renderset.Deca;

/// <summary>
/// Cuándo descarga el QR. Mientras el servicio no ha terminado, siempre:
/// tiene que funcionar durante todo el transporte. Terminado, hasta el
/// plazo configurado (siete días naturales como mínimo).
///
/// Que el QR deje de descargar no borra nada: el DeCA se conserva un año y
/// el emisor lo sigue teniendo en el portal.
/// </summary>
public static class DecaAvailability
{
    public static bool IsPublicDownloadActive(
        DecaDocument deca,
        DateTime nowUtc) =>
        deca.PublicUntilUtc is not { } until ||
        nowUtc < until;

    /// <summary>
    /// Fin del plazo público a partir del fin del servicio: N días naturales
    /// completos en hora de España, hasta la medianoche del último.
    /// </summary>
    public static DateTime PublicUntil(
        DateTime serviceEndedAtUtc,
        DecaOptions options)
    {
        var local =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(serviceEndedAtUtc, DateTimeKind.Utc),
                options.Zone);

        var lastDayEnd =
            local.Date.AddDays(options.EffectivePublicDaysAfterService + 1);

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(lastDayEnd, DateTimeKind.Unspecified),
            options.Zone);
    }

    public static DateTime RetainUntil(
        DateTime issuedAtUtc,
        DecaOptions options) =>
        issuedAtUtc.AddDays(options.EffectiveRetentionDays);
}
