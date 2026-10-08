using System.Text.RegularExpressions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Datos que viajan dentro de un documento emitido con
/// output.includeDocumentData: el JSON del documento
/// (<c>script#rs-document-data</c>) y el de cada tabla
/// (<c>script.dynamic-report-data</c>).
///
/// Los dos bloques los escribe RenderSet (BlazorReportDocumentRenderer y
/// DynamicReportTable) siempre con la misma forma, y su contenido no puede
/// llevar "&lt;/" porque se escapa al escribirlo, así que se pueden quitar
/// con una expresión sin necesidad de parsear el HTML.
/// </summary>
public static partial class DocumentEmbeddedData
{
    /// <summary>
    /// Si el documento lleva algún bloque de datos.
    /// </summary>
    public static bool HasData(
        string? html) =>
        !string.IsNullOrEmpty(html) &&
        DataBlock().IsMatch(html);

    /// <summary>
    /// El documento sin sus bloques de datos. Lo que se ve y se imprime no
    /// cambia: esos bloques nunca se pintan.
    /// </summary>
    public static string Strip(
        string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        return DataBlock().Replace(html, string.Empty);
    }

    [GeneratedRegex(
        """<script\b[^>]*\b(?:id="rs-document-data"|class="dynamic-report-data")[^>]*>.*?</script>\s*""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex DataBlock();
}
