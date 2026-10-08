using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Renderset.Blazor.Components.Rendering;
using Renderset.Core.Rendering;
using Renderset.Core.Resolved;
using Renderset.Core.Themes;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Renderset.Blazor.Rendering;

/// <summary>
/// Genera el HTML del documento con los mismos componentes que pinta el
/// preview del diseñador, usando el renderizador estático de Blazor.
///
/// No hace falta circuito ni interactividad: los componentes de Rendering no
/// tienen ningún manejador de eventos, sólo pintan a partir del informe
/// resuelto y del JSON de datos.
/// </summary>
public sealed class BlazorReportDocumentRenderer
    : IReportDocumentRenderer
{
    private static readonly string DocumentCss =
        LoadDocumentCss();

    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public BlazorReportDocumentRenderer(
        IServiceProvider services,
        ILoggerFactory loggerFactory)
    {
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> RenderHtmlAsync(
        ResolvedReportDefinition report,
        JsonElement data,
        ReportTheme theme,
        string? title = null,
        bool includeDocumentData = false,
        ReportDocumentContext? document = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        await using var renderer =
            new HtmlRenderer(
                _services,
                _loggerFactory);

        var body =
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var parameters =
                    ParameterView.FromDictionary(
                        new Dictionary<string, object?>
                        {
                            [nameof(DynamicReportRenderer.Report)] = report,
                            [nameof(DynamicReportRenderer.Data)] = data,
                            [nameof(DynamicReportRenderer.Theme)] = theme ?? new ReportTheme(),

                            // Los datos de cada tabla viajan con los del
                            // documento: son lo que usa la página del visor
                            // para exportar a CSV. Sin ellos no se incrustan y
                            // el documento pesa la mitad.
                            [nameof(DynamicReportRenderer.IncludeTableData)] = includeDocumentData,

                            // Para el QR de la cabecera (enlace del documento).
                            [nameof(DynamicReportRenderer.Document)] = document
                        });

                var output =
                    await renderer.RenderComponentAsync<DynamicReportRenderer>(
                        parameters);

                return output.ToHtmlString();
            });

        // El JSON del documento se arma aquí y no en el componente porque no
        // es parte de lo que se pinta: es un anexo del documento.
        var documentData =
            includeDocumentData
                ? ResolvedReportDataExporter.ToJson(report, data)
                : null;

        // Pie repetido en cada página al imprimir y en el PDF, con la
        // numeración. Los datos se sustituyen aquí: el pie de cada página se
        // pinta fuera de los componentes y no los tiene.
        var pageFooter =
            PageFooterContent(report.Footer, data);

        var extraHead =
            ReportFontFaces.For(theme);

        return BuildDocument(
            body,
            title ?? report.Name,
            documentData,
            extraHead,
            pageFooter is null
                ? null
                : ReportPageFooter.BuildPrintStyle(pageFooter, theme),
            pageFooter is null
                ? null
                : ReportPageFooter.BuildTemplate(pageFooter, theme));
    }

    private static ReportPageFooter.Content? PageFooterContent(
        ResolvedReportFooter? footer,
        JsonElement data)
    {
        if (footer is null || !footer.HasContent)
            return null;

        var content =
            new ReportPageFooter.Content(
                string.IsNullOrWhiteSpace(footer.Text)
                    ? null
                    : ReportDataTemplate.Apply(footer.Text, data),
                footer.ShowGenerationDate
                    ? DynamicReportFooter.GenerationDateText(DateTime.Now)
                    : null,
                footer.ShowPageNumber
                    ? footer.PageNumberFormat
                    : null);

        return content.IsEmpty
            ? null
            : content;
    }

    private static string BuildDocument(
        string body,
        string title,
        string? documentData,
        string? fontFaces = null,
        string? printStyle = null,
        string? pageFooterTemplate = null)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        builder.Append("<title>")
            .Append(WebUtility.HtmlEncode(title))
            .AppendLine("</title>");

        // El CSS va incrustado, no enlazado: el documento tiene que poder
        // guardarse, enviarse por correo o pasarse a PDF sin depender de que
        // la aplicación esté levantada.
        builder.AppendLine("<style>");

        if (!string.IsNullOrWhiteSpace(fontFaces))
            builder.AppendLine(fontFaces);

        builder.AppendLine(DocumentCss);
        builder.AppendLine("</style>");

        if (!string.IsNullOrWhiteSpace(printStyle))
            builder.AppendLine(printStyle);

        builder.AppendLine("</head>");
        // Sin barra ni scripts: el documento es sólo el documento. Imprimir,
        // PDF, CSV o copiar los datos son cosa de la página que lo enseña
        // (el visor de Web o el de bundles), no del HTML emitido.
        builder.AppendLine("<body class=\"report-document-page\">");

        builder.AppendLine("<div class=\"report-document-sheet\">");
        builder.AppendLine(body);
        builder.AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(documentData))
        {
            builder.AppendLine(
                "<script type=\"application/json\" id=\"rs-document-data\">");

            // Un dato que contenga "</script>" cerraría el bloque y rompería
            // el documento entero.
            builder.AppendLine(
                documentData.Replace("</", "<\\/", StringComparison.Ordinal));

            builder.AppendLine("</script>");
        }

        // Inerte en pantalla: sólo lo lee el paso a PDF (DocumentPdfService).
        if (!string.IsNullOrWhiteSpace(pageFooterTemplate))
            builder.AppendLine(pageFooterTemplate);

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }



    /// <summary>
    /// Nombre lógico fijado en el csproj. Sin él, el nombre del recurso lo
    /// decide MSBuild a partir de la ruta y del root namespace, y cualquier
    /// cambio en cualquiera de los dos deja el documento sin estilos.
    /// </summary>
    private const string CssResourceName =
        "Renderset.Blazor.report-document.css";

    private const string CssAssetPath =
        "wwwroot/_content/Renderset.Blazor/css/report-document.css";

    private static string LoadDocumentCss()
    {
        var css =
            ReadEmbeddedCss()
            ?? ReadCssFromDisk();

        if (!string.IsNullOrWhiteSpace(css))
            return css;

        /*
         * Un documento sin estilos se ve como un borrador y no como lo que
         * enseña el diseñador, así que es mejor romper aquí y decir por qué
         * que entregarlo mudo. El listado de recursos es lo que hace falta
         * para ver si el EmbeddedResource del csproj llegó al ensamblado.
         */
        var available =
            string.Join(
                ", ",
                typeof(BlazorReportDocumentRenderer)
                    .Assembly
                    .GetManifestResourceNames());

        throw new InvalidOperationException(
            "No se ha encontrado el CSS del documento. Comprueba que " +
            "Renderset.Blazor/wwwroot/css/report-document.css existe y que el " +
            "csproj lo incluye como EmbeddedResource con LogicalName " +
            $"'{CssResourceName}'. Recursos embebidos encontrados: " +
            (string.IsNullOrWhiteSpace(available) ? "ninguno" : available) +
            ".");
    }

    private static string? ReadEmbeddedCss()
    {
        var assembly =
            typeof(BlazorReportDocumentRenderer).Assembly;

        var name =
            assembly.GetManifestResourceNames()
                .FirstOrDefault(x =>
                    string.Equals(
                        x,
                        CssResourceName,
                        StringComparison.Ordinal))
            ?? assembly.GetManifestResourceNames()
                .FirstOrDefault(x =>
                    x.EndsWith(
                        "report-document.css",
                        StringComparison.OrdinalIgnoreCase));

        if (name is null)
            return null;

        using var stream =
            assembly.GetManifestResourceStream(name);

        if (stream is null)
            return null;

        using var reader =
            new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Respaldo para cuando el recurso no viaja embebido: el mismo fichero
    /// está en la salida como asset estático de la RCL.
    /// </summary>
    private static string? ReadCssFromDisk()
    {
        var path =
            Path.Combine(
                AppContext.BaseDirectory,
                CssAssetPath.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : null;
    }
}