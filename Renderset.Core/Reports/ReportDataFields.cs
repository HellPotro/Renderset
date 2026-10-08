using Renderset.Core.Definitions;

namespace Renderset.Core.Reports;

/// <summary>
/// Campo del documento que se puede citar por su ruta: en la cabecera
/// ({{data.numero}}), en un bloque o como tipo de contexto de un assignment.
/// </summary>
public sealed record ReportDataFieldOption(
    string Path,
    string Label,
    string Section,
    ReportFieldType Type);

public static class ReportDataFields
{
    /// <summary>
    /// Campos con un único valor por documento: los de la cabecera y los de
    /// las secciones que no repiten. Los de dentro de una sección repetida
    /// (las líneas de cada albarán) no, porque su ruta es relativa a cada
    /// elemento y fuera de él no significa nada.
    /// </summary>
    public static IReadOnlyList<ReportDataFieldOption> SingleValued(
        ReportDefinition? definition)
    {
        var result = new List<ReportDataFieldOption>();

        if (definition is null)
            return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(ReportFieldDefinition field, string section)
        {
            if (string.IsNullOrWhiteSpace(field.DataPath) ||
                !seen.Add(field.DataPath))
            {
                return;
            }

            result.Add(
                new ReportDataFieldOption(
                    field.DataPath,
                    string.IsNullOrWhiteSpace(field.Label) ? field.DataPath : field.Label,
                    section,
                    field.Type));
        }

        foreach (var field in definition.Header?.Fields ?? [])
            Add(field, "Cabecera");

        void Walk(IEnumerable<ReportSectionDefinition> sections)
        {
            foreach (var section in sections.OrderBy(x => x.Order))
            {
                if (!string.IsNullOrWhiteSpace(section.DataPath))
                    continue;

                foreach (var field in section.Fields.OrderBy(x => x.Order))
                    Add(field, section.Name);

                Walk(section.Sections);
            }
        }

        Walk(definition.Sections);

        return result;
    }
}
