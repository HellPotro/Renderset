namespace Renderset.Deca;

/// <summary>
/// Sección "Deca" de la configuración de la API.
///
///     "Deca": {
///       "PublicBaseUrl": "https://deca.renderset.app",
///       "PublicDaysAfterService": 7,
///       "RetentionDays": 365
///     }
///
/// PublicBaseUrl es el dominio que va impreso en el QR de cada DeCA durante
/// un año: corto y para siempre. Tiene que apuntar a la API (que sirve
/// /q/{código}) con HTTPS y TLS 1.2 o superior. Vacío = el de los enlaces
/// compartidos (DocumentSharing:PublicBaseUrl).
/// </summary>
public sealed class DecaOptions
{
    public const string SectionName = "Deca";

    public const string PublicPrefix = "/q";

    /// <summary>
    /// Tamaño máximo del PDF que admite la norma.
    /// </summary>
    public const long MaxPdfBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Mínimo legal de conservación.
    /// </summary>
    public const int MinRetentionDays = 365;

    /// <summary>
    /// Plazo mínimo legal con el enlace activo tras terminar el servicio.
    /// </summary>
    public const int MinPublicDaysAfterService = 7;

    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Días con el QR descargando después de terminar el servicio. La norma
    /// permite desactivarlo a los siete días naturales; nunca menos. Más es
    /// válido (la otra parte lo sigue teniendo), pero cuanto menos tiempo
    /// esté abierto un documento con NIF y matrículas, mejor.
    /// </summary>
    public int PublicDaysAfterService { get; set; } = MinPublicDaysAfterService;

    /// <summary>
    /// Días que se conserva como mínimo. Nunca menos de un año.
    /// </summary>
    public int RetentionDays { get; set; } = MinRetentionDays;

    /// <summary>
    /// Zona horaria del documento: la fecha y hora de emisión que se ve en
    /// el PDF y con la que se comprueba que se emite antes del transporte.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Madrid";

    public int EffectivePublicDaysAfterService =>
        Math.Max(MinPublicDaysAfterService, PublicDaysAfterService);

    public int EffectiveRetentionDays =>
        Math.Max(MinRetentionDays, RetentionDays);

    /// <summary>
    /// URL del QR: https://dominio/q/{código}. Nula si no hay dominio
    /// válido (sólo https; http sólo para localhost, para desarrollar).
    /// </summary>
    public string? PublicUrl(
        string publicCode)
    {
        if (string.IsNullOrWhiteSpace(publicCode) ||
            string.IsNullOrWhiteSpace(PublicBaseUrl) ||
            !Uri.TryCreate(PublicBaseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var allowed =
            uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

        if (!allowed)
            return null;

        // Sólo el dominio: la API sirve /q en la raíz, así que una ruta en
        // PublicBaseUrl ("https://x/deca") daría un QR que no lleva a nada.
        return uri.GetLeftPart(UriPartial.Authority) + PublicPrefix + "/" + Uri.EscapeDataString(publicCode);
    }

    private TimeZoneInfo? _zone;

    public TimeZoneInfo Zone =>
        _zone ??= FindZone(TimeZone);

    private static TimeZoneInfo FindZone(
        string? id)
    {
        // .NET acepta el id IANA también en Windows (ICU); el nombre de
        // Windows queda de reserva para servidores sin ICU.
        foreach (var candidate in new[] { id, "Europe/Madrid", "Romance Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Mejor no arrancar que emitir con la hora mal: la fecha y hora de
        // emisión va en el PDF y decide si se emite antes del transporte.
        // Pasa en contenedores mínimos sin tzdata (instalar tzdata, o
        // InvariantTimezone desactivado).
        throw new InvalidOperationException(
            $"No se encuentra la zona horaria '{id}' (ni Europe/Madrid). " +
            "El servidor necesita los datos de zonas horarias (paquete tzdata en Linux).");
    }
}
