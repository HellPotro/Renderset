namespace Renderset.Core.Localization;

/// <summary>
/// Bandera de una cultura BCP-47, como emoji.
///
/// No hay imágenes ni librería de iconos detrás: una bandera Unicode es el
/// par de indicadores regionales de su código ISO, así que sale del propio
/// código de cultura con una operación aritmética. Meter un CSS de banderas
/// para esto habría añadido una dependencia visual a cambio de nada.
/// </summary>
public static class CultureFlag
{
    /// <summary>
    /// Primer indicador regional del bloque Unicode (U+1F1E6, la 'A').
    /// </summary>
    private const int RegionalIndicatorA = 0x1F1E6;

    /// <summary>
    /// Devuelve la bandera, o cadena vacía si la cultura no lleva región.
    ///
    /// Lo segundo pasa de verdad: "es" a secas es una cultura neutra y
    /// perfectamente válida, y no hay ninguna bandera que le corresponda.
    /// Quien la pinte debe estar preparado para no recibir nada.
    /// </summary>
    public static string For(
        string? culture)
    {
        var region = RegionOf(culture);

        if (region.Length != 2)
            return string.Empty;

        return
            char.ConvertFromUtf32(RegionalIndicatorA + (region[0] - 'A')) +
            char.ConvertFromUtf32(RegionalIndicatorA + (region[1] - 'A'));
    }


    /// <summary>
    /// Código de región en mayúsculas, o cadena vacía si no lo hay.
    ///
    /// Se recorre de derecha a izquierda porque la región es siempre el
    /// último tramo de dos letras: en "zh-Hans-CN" el tramo del medio es el
    /// alfabeto, no el país.
    /// </summary>
    public static string RegionOf(
        string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return string.Empty;

        var parts = culture.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);

        for (var index = parts.Length - 1; index >= 1; index--)
        {
            var part = parts[index];

            if (part.Length == 2 &&
                char.IsAsciiLetter(part[0]) &&
                char.IsAsciiLetter(part[1]))
            {
                return part.ToUpperInvariant();
            }
        }

        return string.Empty;
    }


    /// <summary>
    /// Bandera, o un texto corto de reserva para las culturas neutras, para
    /// que la columna no quede descuadrada cuando falta el emoji.
    /// </summary>
    public static string ForOrCode(
        string? culture)
    {
        var flag = For(culture);

        if (!string.IsNullOrEmpty(flag))
            return flag;

        return string.IsNullOrWhiteSpace(culture)
            ? string.Empty
            : culture.Split(['-', '_'])[0].ToUpperInvariant();
    }
}
