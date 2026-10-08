using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Renderset.Core.Data;
using Renderset.Core.Definitions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Datos del documento dentro de un texto: {{data.ruta}}.
///
///     "Albarán {{data.numero}}"          → "Albarán 3116"
///     "Cliente: {{data.cliente.nombre}}" → "Cliente: Talleres Peláez"
///
/// Es la misma sintaxis que ya admitía el nombre del fichero. Convive con las
/// variables del tenant ({{company.cif}}): las variables se sustituyen al
/// construir el diccionario y los marcadores data. se dejan pasar tal cual,
/// porque los datos sólo se conocen al pintar. Por eso esto se aplica en los
/// componentes del documento, que son los que tienen los datos.
///
/// Un dato que no existe se queda en blanco: en una cabecera es mejor un
/// hueco que un "{{data.numero}}" impreso.
/// </summary>
public static partial class ReportDataTemplate
{
    public const string Prefix = "data.";

    private static readonly IReportDataAccessor Accessor =
        new JsonReportDataAccessor();

    [GeneratedRegex(@"\{\{\s*data\.(?<path>[^{}]+?)\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex Token();

    public static bool HasTokens(
        string? text) =>
        !string.IsNullOrEmpty(text) &&
        text.Contains("{{", StringComparison.Ordinal) &&
        Token().IsMatch(text);

    /// <summary>
    /// Marcador para insertar desde los editores: {{data.cliente.codigo}}.
    /// </summary>
    public static string TokenFor(
        string dataPath) =>
        "{{" + Prefix + dataPath.Trim() + "}}";

    /// <summary>
    /// Rutas de los marcadores del texto, sin el prefijo.
    /// </summary>
    public static IReadOnlyList<string> Paths(
        string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : Token()
                .Matches(text)
                .Select(x => x.Groups["path"].Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    /// <summary>
    /// Sustituye los marcadores con el valor del documento. Sin datos (un
    /// bloque visto fuera de un report) decide <paramref name="onMissing"/>;
    /// por defecto, en blanco.
    /// </summary>
    public static string Apply(
        string? text,
        JsonElement data,
        Func<string, string>? onMissing = null,
        CultureInfo? culture = null)
    {
        if (!HasTokens(text))
            return text ?? string.Empty;

        return Token().Replace(
            text!,
            match =>
            {
                var path = match.Groups["path"].Value.Trim();

                var value =
                    TryFormat(data, path, culture);

                if (value is not null)
                    return value;

                return onMissing is null
                    ? string.Empty
                    : onMissing(path);
            });
    }

    /// <summary>
    /// Valor como se lee en un documento: las fechas en formato corto del
    /// idioma, los números tal cual vienen (un código 3116 no es 3.116,00).
    /// </summary>
    public static string? TryFormat(
        JsonElement data,
        string path,
        CultureInfo? culture = null)
    {
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        if (!Accessor.TryGetValue(data, path, out var value) ||
            value is null or JsonElement)
        {
            return null;
        }

        var text =
            value is DateTime or DateTimeOffset
                ? ReportValueFormatter.Format(value, ReportFieldType.Date, culture)
                : ReportValueFormatter.AsText(value);

        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
    }
}
