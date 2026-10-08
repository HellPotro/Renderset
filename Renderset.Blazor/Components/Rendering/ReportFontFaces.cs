using System.Collections.Concurrent;
using System.Text;
using Renderset.Core.Themes;

namespace Renderset.Blazor.Rendering;

/// <summary>
/// @font-face con la tipografía del tema incrustada en base64, para el
/// documento emitido.
///
/// El documento tiene que verse igual en el visor, en el PDF (Gotenberg, sin
/// red garantizada) y guardado o enviado por correo, así que no puede
/// enlazar la fuente: la lleva dentro. Sólo la que usa, en latín, normal y
/// negrita (40-100 KB).
///
/// Los .woff2 viajan embebidos en el ensamblado (csproj, LogicalName
/// Renderset.Blazor.fonts.*): la API no sirve el wwwroot de la RCL.
/// </summary>
public static class ReportFontFaces
{
    private const string ResourcePrefix = "Renderset.Blazor.fonts.";

    private static readonly int[] Weights = [400, 700];

    private static readonly ConcurrentDictionary<string, string> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// CSS a incrustar en el &lt;style&gt; del documento, o vacío si la
    /// tipografía del tema es del sistema.
    /// </summary>
    public static string For(
        ReportTheme? theme)
    {
        var font =
            ReportThemeFonts.FindWebFont(theme?.FontFamily);

        return font is null
            ? string.Empty
            : Cache.GetOrAdd(font.Family, _ => Build(font));
    }

    private static string Build(
        ReportThemeFonts.WebFont font)
    {
        var css = new StringBuilder();

        foreach (var weight in Weights)
        {
            var bytes =
                Read($"{font.File}-latin-{weight}-normal.woff2");

            // Un fichero que falte no rompe el documento: se pinta con la
            // pila de reserva del tema.
            if (bytes is null)
                continue;

            css.Append("@font-face{font-family:\"")
                .Append(font.Family)
                .Append("\";font-style:normal;font-weight:")
                .Append(weight)
                .Append(";src:url(data:font/woff2;base64,")
                .Append(Convert.ToBase64String(bytes))
                .AppendLine(") format(\"woff2\");}");
        }

        return css.ToString();
    }

    private static byte[]? Read(
        string fileName)
    {
        var assembly =
            typeof(ReportFontFaces).Assembly;

        using var stream =
            assembly.GetManifestResourceStream(ResourcePrefix + fileName);

        if (stream is null)
            return null;

        using var memory = new MemoryStream();

        stream.CopyTo(memory);

        return memory.ToArray();
    }
}
