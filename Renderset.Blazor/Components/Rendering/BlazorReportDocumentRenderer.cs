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
        bool includeToolbar = false,
        bool includeDocumentData = false,
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

                            // Los datos sólo viajan si hay barra: sin ella
                            // nadie los usa y duplicarían el peso del
                            // documento para nada.
                            [nameof(DynamicReportRenderer.IncludeTableData)] = includeToolbar
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

        return BuildDocument(
            body,
            title ?? report.Name,
            includeToolbar,
            documentData);
    }

    private static string BuildDocument(
        string body,
        string title,
        bool includeToolbar,
        string? documentData)
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
        builder.AppendLine(DocumentCss);
        builder.AppendLine("</style>");

        builder.AppendLine("</head>");
        builder.AppendLine("<body class=\"report-document-page\">");

        if (includeToolbar)
            builder.AppendLine(ToolbarHtml);

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

        if (includeToolbar)
        {
            builder.AppendLine("<script>");
            builder.AppendLine(ToolbarScript);
            builder.AppendLine("</script>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }


    /// <summary>
    /// Barra de acciones del documento.
    ///
    /// Los menús se llenan en tiempo de ejecución a partir de los datos que
    /// acompañan a cada tabla, porque hasta que el documento no está montado
    /// no se sabe cuántas tablas tiene ni cómo se llaman.
    /// </summary>
    private const string ToolbarHtml =
        """
        <div class="report-toolbar" data-report-toolbar>

            <div class="report-toolbar-inner">

                <div class="report-toolbar-menu" data-menu="csv" hidden>
                    <button type="button" class="report-toolbar-button" data-menu-toggle>
                        Exportar CSV
                    </button>
                    <div class="report-toolbar-panel" data-menu-panel></div>
                </div>

                <button type="button" class="report-toolbar-button" data-copy-document hidden>
                    Copiar JSON
                </button>

                <button type="button" class="report-toolbar-button is-primary" data-print>
                    Imprimir
                </button>

            </div>

            <div class="report-toolbar-note" data-note hidden></div>

        </div>
        """;

    /// <summary>
    /// Script de la barra. Va incrustado y sin dependencias por la misma
    /// razón que el CSS: el documento tiene que funcionar guardado en disco
    /// o enviado por correo, sin que la aplicación esté levantada.
    /// </summary>
    private const string ToolbarScript =
        """
        (function () {

            var toolbar = document.querySelector("[data-report-toolbar]");

            if (!toolbar)
                return;

            var culture = document.documentElement.lang || "es";

            /* Una tabla dentro de una sección que se repite se pinta una vez
               por elemento, así que hay varios bloques con el mismo id. Se
               juntan: exportar "Líneas" de una factura con tres albaranes
               tiene que dar todas las líneas, no las del primero. */
            function readTables() {

                var blocks = document.querySelectorAll("script.dynamic-report-data");
                var byId = {};
                var order = [];

                for (var i = 0; i < blocks.length; i++) {

                    var payload;

                    try {
                        payload = JSON.parse(blocks[i].textContent);
                    } catch (e) {
                        continue;
                    }

                    if (!byId[payload.id]) {
                        byId[payload.id] = {
                            id: payload.id,
                            title: payload.title || payload.id,
                            columns: payload.columns || [],
                            rows: []
                        };
                        order.push(payload.id);
                    }

                    byId[payload.id].rows =
                        byId[payload.id].rows.concat(payload.rows || []);
                }

                return order.map(function (id) { return byId[id]; });
            }

            function formatCell(value) {

                if (value === null || value === undefined)
                    return "";

                /* Los números salen con los decimales del idioma del
                   documento y sin separador de miles: es lo único que una
                   hoja de cálculo reconoce como número al pegarlo. */
                if (typeof value === "number")
                    return value.toLocaleString(culture, {
                        useGrouping: false,
                        maximumFractionDigits: 6
                    });

                if (typeof value === "boolean")
                    return value ? "1" : "0";

                var text = String(value);

                /* Una fecha ISO se deja en ISO: es el formato que cualquier
                   hoja de cálculo entiende sin pelearse con el idioma. */
                return text;
            }

            function toCsv(table) {

                var lines = [];

                lines.push(table.columns.map(quote).join(";"));

                for (var i = 0; i < table.rows.length; i++) {

                    var row = table.rows[i];

                    lines.push(
                        table.columns
                            .map(function (column) {
                                return quote(formatCell(row[column]));
                            })
                            .join(";"));
                }

                return lines.join("\r\n");
            }

            function quote(value) {
                return '"' + String(value === undefined ? "" : value).replace(/"/g, '""') + '"';
            }

            function download(name, content) {

                /* El BOM es lo que hace que Excel abra el fichero como UTF-8
                   en vez de romper los acentos. */
                var blob = new Blob(["\ufeff" + content], {
                    type: "text/csv;charset=utf-8;"
                });

                var url = URL.createObjectURL(blob);
                var link = document.createElement("a");

                link.href = url;
                link.download = name;

                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);

                URL.revokeObjectURL(url);
            }

            function fileName(table) {
                return (table.title || table.id)
                    .replace(/[^\w\-]+/g, "-")
                    .toLowerCase() + ".csv";
            }

            function note(message) {

                var element = toolbar.querySelector("[data-note]");

                element.textContent = message;
                element.hidden = false;

                window.setTimeout(function () {
                    element.hidden = true;
                }, 2500);
            }

            function copy(text, message) {

                /* El portapapeles moderno sólo funciona en contextos
                   seguros. Un documento abierto desde el disco no lo es, y
                   ahí es justo donde interesa que siga funcionando. */
                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(text).then(function () {
                        note(message);
                    });
                    return;
                }

                var area = document.createElement("textarea");

                area.value = text;
                area.setAttribute("readonly", "");
                area.style.position = "fixed";
                area.style.opacity = "0";

                document.body.appendChild(area);
                area.select();

                try {
                    document.execCommand("copy");
                    note(message);
                } catch (e) {
                    note("No se ha podido copiar.");
                }

                document.body.removeChild(area);
            }

            function closeMenus() {
                var panels = toolbar.querySelectorAll("[data-menu]");
                for (var i = 0; i < panels.length; i++)
                    panels[i].classList.remove("is-open");
            }

            function buildMenu(name, tables, action, allLabel) {

                var menu = toolbar.querySelector('[data-menu="' + name + '"]');

                if (!tables.length)
                    return;

                menu.hidden = false;

                var panel = menu.querySelector("[data-menu-panel]");

                tables.forEach(function (table) {

                    var button = document.createElement("button");

                    button.type = "button";
                    button.textContent =
                        table.title + " (" + table.rows.length + ")";

                    button.addEventListener("click", function () {
                        closeMenus();
                        action([table]);
                    });

                    panel.appendChild(button);
                });

                if (tables.length > 1) {

                    var all = document.createElement("button");

                    all.type = "button";
                    all.className = "is-all";
                    all.textContent = allLabel;

                    all.addEventListener("click", function () {
                        closeMenus();
                        action(tables);
                    });

                    panel.appendChild(all);
                }

                menu.querySelector("[data-menu-toggle]")
                    .addEventListener("click", function (e) {

                        e.stopPropagation();

                        var open = menu.classList.contains("is-open");

                        closeMenus();

                        if (!open)
                            menu.classList.add("is-open");
                    });
            }

            var tables = readTables();

            buildMenu("csv", tables, function (selection) {
                selection.forEach(function (table) {
                    download(fileName(table), toCsv(table));
                });
            }, "Todas las tablas");

            /* El JSON del documento sólo existe si quien pidió el render lo
               activó. Sin él no hay botón: es preferible que falte a que esté
               y no haga nada. */
            var documentData = document.getElementById("rs-document-data");

            if (documentData) {

                var copyButton = toolbar.querySelector("[data-copy-document]");

                copyButton.hidden = false;

                copyButton.addEventListener("click", function () {
                    closeMenus();
                    copy(
                        documentData.textContent.trim(),
                        "Datos del documento copiados");
                });
            }

            toolbar.querySelector("[data-print]")
                .addEventListener("click", function () {
                    closeMenus();
                    window.print();
                });

            document.addEventListener("click", closeMenus);

        })();
        """;

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