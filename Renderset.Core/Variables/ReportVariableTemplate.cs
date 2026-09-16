using System.Text.RegularExpressions;

namespace Renderset.Core.Variables;

/// <summary>
/// Sustitución de {{marcadores}} por el valor de las variables del tenant.
///
/// Está aquí y no en el resolver de base de datos porque la misma operación
/// hace falta en tres sitios: la API al servir un bloque resuelto, el preview
/// del editor de bloques y el diccionario del diseñador. Tener tres regex
/// parecidas era la forma segura de que acabaran comportándose distinto.
/// </summary>
public static class ReportVariableTemplate
{
    private static readonly Regex TokenRegex = new(
        @"\{\{\s*(?<key>[^{}]+?)\s*\}\}",
        RegexOptions.Compiled);

    public static bool HasTokens(
        string? text)
    {
        return !string.IsNullOrEmpty(text) &&
               text.Contains("{{", StringComparison.Ordinal);
    }

    /// <summary>
    /// Sustituye los marcadores con valor. Para los que no lo tienen decide
    /// <paramref name="onMissing"/>: por defecto se deja el marcador tal cual,
    /// que es lo que hace la API, y el preview lo usa para marcarlos en
    /// pantalla.
    /// </summary>
    public static string Apply(
        string? text,
        IReadOnlyDictionary<string, string>? values,
        Func<string, string>? onMissing = null)
    {
        if (!HasTokens(text))
            return text ?? string.Empty;

        return TokenRegex.Replace(
            text!,
            match =>
            {
                var key =
                    match.Groups["key"]
                        .Value
                        .Trim();

                if (values is not null &&
                    values.TryGetValue(key, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return onMissing is null
                    ? match.Value
                    : onMissing(key);
            });
    }

    public static Dictionary<string, string> ToDictionary(
        IEnumerable<ReportVariable>? variables)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var variable in variables ?? [])
        {
            if (string.IsNullOrWhiteSpace(variable.Key))
                continue;

            result[variable.Key] = variable.Value ?? string.Empty;
        }

        return result;
    }
}
