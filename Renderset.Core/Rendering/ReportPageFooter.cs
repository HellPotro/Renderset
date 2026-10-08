using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Renderset.Core.Themes;

namespace Renderset.Core.Rendering;

/// <summary>
/// Pie de página repetido en cada hoja, con "Página 1 de 3".
///
/// En pantalla el pie del documento va pegado al final de la hoja (CSS). Al
/// imprimir tiene que salir en todas las páginas, y eso no lo puede hacer el
/// HTML del cuerpo: lo hace el motor de impresión en el margen inferior. Hay
/// dos motores y cada uno necesita lo suyo, así que el documento emitido
/// lleva las dos cosas, generadas aquí a partir del mismo contenido:
///
///   - &lt;template id="rs-page-footer"&gt;: el pie como documento HTML
///     autosuficiente, que es lo que Gotenberg (Chromium) pinta en el margen
///     de cada página del PDF, con pageNumber / totalPages.
///   - &lt;style id="rs-page-print"&gt;: reglas @page con cajas de margen para
///     cuando se imprime desde el navegador (botón Imprimir del visor).
///
/// Al pasar a PDF, <see cref="Pdf.DocumentPdfService"/> saca el template,
/// quita el estilo de impresión (o saldría el pie dos veces) y se lo da al
/// conversor con el margen inferior que necesita.
///
/// Los documentos emitidos antes no llevan nada de esto y siguen saliendo
/// como siempre: numeración "1 / 3" de la configuración de la API.
/// </summary>
public static partial class ReportPageFooter
{
    public const string TemplateId = "rs-page-footer";

    public const string PrintStyleId = "rs-page-print";

    /// <summary>
    /// Márgenes de la hoja al imprimir desde el navegador. Son los mismos que
    /// usa la API por defecto para el PDF.
    /// </summary>
    public const double SideMarginMm = 14;

    public const double TopMarginMm = 14;

    public const double MinBottomMarginMm = 16;

    public const double MaxBottomMarginMm = 60;

    /// <summary>
    /// Ancho útil aproximado de un A4 con márgenes, en caracteres de 8 px,
    /// para calcular cuántas líneas ocupa un texto legal largo.
    /// </summary>
    private const int CharactersPerLine = 120;

    private const double LineHeightMm = 3.1;

    private const double PaddingMm = 9;

    public sealed record Content(
        string? Text,
        string? GenerationDate,
        string? PageNumberFormat)
    {
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Text) &&
            string.IsNullOrWhiteSpace(GenerationDate) &&
            string.IsNullOrWhiteSpace(PageNumberFormat);
    }

    /// <summary>
    /// Alto que necesita el pie en el margen inferior.
    /// </summary>
    public static double HeightMm(
        Content content)
    {
        var lines = 0;

        if (!string.IsNullOrWhiteSpace(content.Text))
        {
            foreach (var line in content.Text.Replace("\r\n", "\n").Split('\n'))
                lines += Math.Max(1, (int)Math.Ceiling(line.Length / (double)CharactersPerLine));
        }

        // Fecha y numeración van en la misma línea, a la derecha.
        lines = Math.Max(lines, 1);

        return Math.Clamp(
            Math.Round(PaddingMm + lines * LineHeightMm, 1),
            MinBottomMarginMm,
            MaxBottomMarginMm);
    }

    // ------------------------------------------------------------ documento

    /// <summary>
    /// Lo que se añade al final del &lt;body&gt; del documento emitido.
    /// </summary>
    public static string BuildTemplate(
        Content content,
        ReportTheme? theme)
    {
        theme ??= new ReportTheme();

        var height = HeightMm(content);

        var builder = new StringBuilder();

        builder.Append("<template id=\"")
            .Append(TemplateId)
            .Append("\" data-height-mm=\"")
            .Append(Number(height))
            .AppendLine("\">");

        builder.AppendLine(FooterDocument(content, theme));

        builder.AppendLine("</template>");

        return builder.ToString();
    }

    /// <summary>
    /// Pie como documento HTML independiente. En el margen de Chromium no
    /// llega ningún estilo de la página, así que todo va en línea, y el
    /// tamaño de letra tiene que ir en px explícitos.
    /// </summary>
    private static string FooterDocument(
        Content content,
        ReportTheme theme)
    {
        var muted = SafeColor(theme.MutedTextColor, "#6D7B83");
        var border = SafeColor(theme.BorderColor, "#D8E1E7");
        var font = ReportThemeRules.IsFontFamily(theme.FontFamily)
            ? theme.FontFamily.Trim()
            : "Arial, sans-serif";

        var builder = new StringBuilder();

        builder.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>");
        builder.Append("html,body{margin:0;padding:0;width:100%;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
        builder.Append(".rs-pf{box-sizing:border-box;width:100%;padding:3mm ")
            .Append(Number(SideMarginMm))
            .Append("mm 0;display:flex;align-items:flex-start;gap:16px;")
            .Append("font-family:").Append(font).Append(';')
            .Append("font-size:8px;line-height:1.35;color:").Append(muted).Append(";}");
        builder.Append(".rs-pf-in{display:flex;align-items:flex-start;gap:16px;width:100%;padding-top:2mm;border-top:1px solid ")
            .Append(border).Append(";}");
        builder.Append(".rs-pf-text{flex:1;white-space:pre-line;}");
        builder.Append(".rs-pf-meta{margin-left:auto;white-space:nowrap;text-align:right;}");
        builder.Append("</style></head><body><div class=\"rs-pf\"><div class=\"rs-pf-in\">");

        if (!string.IsNullOrWhiteSpace(content.Text))
        {
            builder.Append("<div class=\"rs-pf-text\">")
                .Append(WebUtility.HtmlEncode(content.Text.Trim()))
                .Append("</div>");
        }

        var meta = new List<string>();

        if (!string.IsNullOrWhiteSpace(content.GenerationDate))
            meta.Add(WebUtility.HtmlEncode(content.GenerationDate.Trim()));

        if (!string.IsNullOrWhiteSpace(content.PageNumberFormat))
            meta.Add(PageNumberHtml(content.PageNumberFormat));

        if (meta.Count > 0)
        {
            builder.Append("<div class=\"rs-pf-meta\">")
                .Append(string.Join(" &middot; ", meta))
                .Append("</div>");
        }

        builder.Append("</div></div></body></html>");

        return builder.ToString();
    }

    /// <summary>
    /// "Página {page} de {pages}" con los huecos que rellena Chromium.
    /// </summary>
    private static string PageNumberHtml(
        string format) =>
        WebUtility.HtmlEncode(format.Trim())
            .Replace("{page}", "<span class=\"pageNumber\"></span>", StringComparison.OrdinalIgnoreCase)
            .Replace("{pages}", "<span class=\"totalPages\"></span>", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reglas @page para imprimir desde el navegador: márgenes y el pie en
    /// las cajas de margen inferiores. Chrome y Edge las pintan desde la
    /// versión 131 (con "Encabezados y pies de página" desmarcado en el
    /// diálogo de impresión).
    /// </summary>
    public static string BuildPrintStyle(
        Content content,
        ReportTheme? theme)
    {
        theme ??= new ReportTheme();

        var muted = SafeColor(theme.MutedTextColor, "#6D7B83");
        var font = ReportThemeRules.IsFontFamily(theme.FontFamily)
            ? theme.FontFamily.Trim()
            : "Arial, sans-serif";

        var common =
            $"font-family:{font};font-size:7.5pt;color:{muted};vertical-align:top;padding-top:3mm;";

        var builder = new StringBuilder();

        builder.Append("<style id=\"").Append(PrintStyleId).AppendLine("\">");
        builder.Append("@page{margin:")
            .Append(Number(TopMarginMm)).Append("mm ")
            .Append(Number(SideMarginMm)).Append("mm ")
            .Append(Number(HeightMm(content))).Append("mm ")
            .Append(Number(SideMarginMm)).Append("mm;");

        if (!string.IsNullOrWhiteSpace(content.Text))
        {
            builder.Append("@bottom-left{content:")
                .Append(CssString(content.Text.Trim()))
                .Append(';')
                .Append(common)
                .Append("white-space:pre-wrap;text-align:left;width:70%;}");
        }

        var meta = new List<string>();

        if (!string.IsNullOrWhiteSpace(content.GenerationDate))
            meta.Add(CssString(content.GenerationDate.Trim()));

        if (!string.IsNullOrWhiteSpace(content.PageNumberFormat))
            meta.Add(PageNumberCss(content.PageNumberFormat));

        if (meta.Count > 0)
        {
            builder.Append("@bottom-right{content:")
                .Append(string.Join(" \" \\B7  \" ", meta))
                .Append(';')
                .Append(common)
                .Append("text-align:right;white-space:nowrap;}");
        }

        builder.AppendLine("}");
        builder.AppendLine("</style>");

        return builder.ToString();
    }

    private static string PageNumberCss(
        string format)
    {
        var parts = new List<string>();

        foreach (var piece in PageToken().Split(format.Trim()))
        {
            if (piece.Length == 0)
                continue;

            if (piece.Equals("{page}", StringComparison.OrdinalIgnoreCase))
                parts.Add("counter(page)");
            else if (piece.Equals("{pages}", StringComparison.OrdinalIgnoreCase))
                parts.Add("counter(pages)");
            else
                parts.Add(CssString(piece));
        }

        return parts.Count == 0
            ? "counter(page)"
            : string.Join(' ', parts);
    }

    [GeneratedRegex(@"(\{pages?\})", RegexOptions.IgnoreCase)]
    private static partial Regex PageToken();

    /// <summary>
    /// Literal de CSS: sin comillas ni barras que lo cierren, saltos de línea
    /// como \A y "&lt;" escapado para que el texto no pueda cerrar el
    /// &lt;style&gt;.
    /// </summary>
    private static string CssString(
        string value)
    {
        var builder = new StringBuilder("\"");

        foreach (var c in value.Replace("\r\n", "\n"))
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\A "); break;
                case '<': builder.Append("\\3C "); break;
                case '>': builder.Append("\\3E "); break;
                default:
                    if (!char.IsControl(c))
                        builder.Append(c);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    // ------------------------------------------------------------------ PDF

    public sealed record Extracted(
        string Html,
        string FooterHtml,
        double HeightMm);

    /// <summary>
    /// Saca el pie del documento emitido para el conversor de PDF. Nulo si el
    /// documento no lo lleva (emitido antes de existir, o sin pie).
    /// </summary>
    public static Extracted? Extract(
        string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        var match = TemplateBlock().Match(html);

        if (!match.Success)
            return null;

        var height =
            double.TryParse(
                match.Groups["height"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? Math.Clamp(parsed, MinBottomMarginMm, MaxBottomMarginMm)
                : MinBottomMarginMm;

        var withoutTemplate =
            html.Remove(match.Index, match.Length);

        var withoutPrintStyle =
            PrintStyleBlock().Replace(withoutTemplate, string.Empty, 1);

        return new Extracted(
            withoutPrintStyle,
            match.Groups["body"].Value.Trim(),
            height);
    }

    [GeneratedRegex("<template id=\"rs-page-footer\" data-height-mm=\"(?<height>[0-9.]+)\">(?<body>.*?)</template>", RegexOptions.Singleline)]
    private static partial Regex TemplateBlock();

    [GeneratedRegex("<style id=\"rs-page-print\">.*?</style>", RegexOptions.Singleline)]
    private static partial Regex PrintStyleBlock();

    // --------------------------------------------------------------- utils

    private static string SafeColor(
        string? value,
        string fallback) =>
        ReportThemeRules.IsColor(value)
            ? value!.Trim()
            : fallback;

    private static string Number(
        double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
