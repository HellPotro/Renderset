using Renderset.Core.Data;
using Renderset.Core.Resolved;
using System.Text.Json;

namespace Renderset.Core.Rendering;

/// <summary>
/// Proyecta un informe ya resuelto a JSON, para quien quiera integrar los
/// datos del documento en otro sistema.
///
/// Dos decisiones que la separan del JSON que acompaña a cada tabla:
///
/// Las claves son los IDENTIFICADORES de campo y de columna, no sus
/// etiquetas. Las etiquetas salen del diccionario, así que el mismo
/// documento en inglés produciría propiedades distintas y rompería al
/// integrador sin que nadie hubiera tocado nada. Los ids son estables por
/// diseño: no se recalculan aunque el origen renombre un campo.
///
/// Y sólo sale lo visible. Si la configuración oculta el coste, el coste no
/// viaja: un documento que se comparte no puede entregar por detrás lo que
/// se decidió no enseñar por delante.
/// </summary>
public static class ResolvedReportDataExporter
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,

            Encoder = System.Text.Encodings.Web.JavaScriptEncoder
                .UnsafeRelaxedJsonEscaping
        };


    public static string ToJson(
        ResolvedReportDefinition report,
        JsonElement data,
        IReportDataAccessor? accessor = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var reader =
            accessor ?? new JsonReportDataAccessor();

        var document =
            new Dictionary<string, object?>
            {
                ["reportId"] = report.Id,
                ["name"] = report.Name,
                ["version"] = report.Version
            };

        if (report.Header is { Visible: true })
        {
            var header = ReadFields(report.Header.Fields, data, reader);

            if (header.Count > 0)
                document["header"] = header;
        }

        var sections = new Dictionary<string, object?>();

        // Se recorre el cuerpo y no la lista de secciones porque el cuerpo es
        // lo que decide qué se pinta y en qué orden. Una sección anidada la
        // saca su padre, y así no aparece dos veces.
        foreach (var item in VisibleBodySections(report))
        {
            sections[item.Id] =
                ReadSection(item, data, reader);
        }

        if (sections.Count > 0)
            document["sections"] = sections;

        return JsonSerializer.Serialize(document, Options);
    }


    /// <summary>
    /// Una sección con DataPath es una colección y sale como array; sin él
    /// se pinta una vez y sale como objeto. La forma del JSON acompaña a la
    /// del documento, que es lo que quien lo lee tiene delante.
    /// </summary>
    private static object ReadSection(
        ResolvedReportSection section,
        JsonElement data,
        IReportDataAccessor reader)
    {
        if (string.IsNullOrWhiteSpace(section.DataPath))
            return ReadSectionItem(section, data, reader);

        return reader
            .GetCollection(data, section.DataPath)
            .Select(item => ReadSectionItem(section, item, reader))
            .ToList();
    }


    private static Dictionary<string, object?> ReadSectionItem(
        ResolvedReportSection section,
        JsonElement item,
        IReportDataAccessor reader)
    {
        var result =
            ReadFields(section.Fields, item, reader);

        if (section.Table is not null)
        {
            result[section.Table.Id] =
                ReadTable(section.Table, item, reader);
        }

        foreach (var child in VisibleSections(section.Sections))
            result[child.Id] = ReadSection(child, item, reader);

        return result;
    }


    private static List<Dictionary<string, object?>> ReadTable(
        ResolvedReportTable table,
        JsonElement item,
        IReportDataAccessor reader)
    {
        var columns =
            table.Columns
                .Where(x => x.Visible)
                .OrderBy(x => x.Order)
                .ToList();

        return reader
            .GetCollection(item, table.DataPath)
            .Select(row =>
            {
                var values =
                    new Dictionary<string, object?>();

                foreach (var column in columns)
                {
                    reader.TryGetValue(
                        row,
                        column.DataPath,
                        out var value);

                    values[column.Id] = value;
                }

                return values;
            })
            .ToList();
    }


    private static Dictionary<string, object?> ReadFields(
        IEnumerable<ResolvedReportField> fields,
        JsonElement item,
        IReportDataAccessor reader)
    {
        var values =
            new Dictionary<string, object?>();

        foreach (var field in fields.Where(x => x.Visible).OrderBy(x => x.Order))
        {
            reader.TryGetValue(
                item,
                field.DataPath,
                out var value);

            values[field.Id] = value;
        }

        return values;
    }


    private static IEnumerable<ResolvedReportSection> VisibleSections(
        IEnumerable<ResolvedReportSection> sections) =>
        sections
            .Where(x => x.Visible)
            .OrderBy(x => x.Order);


    private static IEnumerable<ResolvedReportSection> VisibleBodySections(
        ResolvedReportDefinition report) =>
        report.Body
            .Where(x =>
                x.Type == Definitions.ReportBodyItemType.Section &&
                x.Section is { Visible: true })
            .OrderBy(x => x.Row)
            .ThenBy(x => x.Order)
            .Select(x => x.Section!);
}
