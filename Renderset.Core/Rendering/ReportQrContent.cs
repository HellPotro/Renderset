using System.Text.Json;
using System.Text.RegularExpressions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Texto que lleva el QR de la cabecera.
///
/// Es una plantilla con marcadores entre llaves:
///
///     {documentUrl}   enlace público del documento (por defecto): lo abre
///                     cualquiera, sin cuenta
///     {viewerUrl}     visor interno de RenderSet Web (pide sesión)
///     {documentId}    id del documento emitido
///     {campo}         cualquier dato del documento por su ruta: {salidaid},
///                     {cliente.codigo}...
///
/// Así sirve tanto para "el link del documento" como para apuntar a una
/// pantalla del ERP: https://erp/salidas/{salidaid}.
///
/// Si un marcador no se puede resolver, al emitir no hay QR (mejor ninguno
/// que uno que lleva a un sitio equivocado). En el preview se pinta con un
/// valor de muestra para ver cómo queda.
/// </summary>
public static partial class ReportQrContent
{
    public const string DocumentUrlTemplate = "{documentUrl}";

    /// <summary>
    /// Lo que cabe con holgura en un QR legible impreso a tamaño de
    /// cabecera. Más largo, el código sale tan denso que no se lee.
    /// </summary>
    public const int MaxLength = 600;

    private const string DocumentUrlToken = "documentUrl";
    private const string DocumentIdToken = "documentId";
    private const string ViewerUrlToken = "viewerUrl";

    public static string? Resolve(
        string? template,
        JsonElement data,
        ReportDocumentContext? document)
    {
        template = string.IsNullOrWhiteSpace(template)
            ? DocumentUrlTemplate
            : template.Trim();

        var preview = document?.IsPreview == true;
        var missing = false;

        // En una URL los datos van escapados para no romperla; en un texto
        // libre ("Salida 3116") se dejan tal cual.
        var isUrl = template.Contains("://", StringComparison.Ordinal) ||
                    template.StartsWith('{' + DocumentUrlToken, StringComparison.OrdinalIgnoreCase) ||
                    template.StartsWith('{' + ViewerUrlToken, StringComparison.OrdinalIgnoreCase);

        var text =
            Token().Replace(template, match =>
            {
                var name = match.Groups[1].Value.Trim();

                var value =
                    name.Equals(DocumentUrlToken, StringComparison.OrdinalIgnoreCase)
                        ? document?.DocumentUrl ?? (preview ? "https://renderset/d/vista-previa" : null)
                        : name.Equals(ViewerUrlToken, StringComparison.OrdinalIgnoreCase)
                            ? document?.ViewerUrl ?? (preview ? "https://renderset/documents/vista-previa" : null)
                        : name.Equals(DocumentIdToken, StringComparison.OrdinalIgnoreCase)
                            ? document?.DocumentId ?? (preview ? "vista-previa" : null)
                            : DataValue(data, name, isUrl);

                if (value is null && preview)
                    value = name;

                if (value is null)
                {
                    missing = true;
                    return string.Empty;
                }

                return value;
            });

        if (missing || string.IsNullOrWhiteSpace(text))
            return null;

        return text.Length > MaxLength
            ? null
            : text;
    }

    /// <summary>
    /// Marcadores de la plantilla que no son de documento, para avisar en el
    /// editor de los que no existen en los datos.
    /// </summary>
    public static IReadOnlyList<string> DataTokens(
        string? template) =>
        string.IsNullOrWhiteSpace(template)
            ? []
            : Token()
                .Matches(template)
                .Select(x => x.Groups[1].Value.Trim())
                .Where(x =>
                    !x.Equals(DocumentUrlToken, StringComparison.OrdinalIgnoreCase) &&
                    !x.Equals(ViewerUrlToken, StringComparison.OrdinalIgnoreCase) &&
                    !x.Equals(DocumentIdToken, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string? DataValue(
        JsonElement data,
        string path,
        bool escape)
    {
        // Se lee el JSON tal cual y no con el accessor de los campos: aquí
        // interesa el valor como viene ("2026-10-06", "3116"), no convertido
        // a fecha o a número y vuelto a escribir.
        if (!TryGetElement(data, path, out var element))
            return null;

        var text =
            element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
                _ => null
            };

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return escape
            ? Uri.EscapeDataString(text.Trim())
            : text.Trim();
    }

    private static bool TryGetElement(
        JsonElement data,
        string path,
        out JsonElement result)
    {
        result = data;

        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (result.ValueKind != JsonValueKind.Object)
                return false;

            var found = false;

            foreach (var property in result.EnumerateObject())
            {
                if (property.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                {
                    result = property.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"\{([A-Za-z0-9_.\- ]+)\}")]
    private static partial Regex Token();
}
