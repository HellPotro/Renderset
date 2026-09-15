using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// Deriva el <see cref="ReportDataSchema"/> a partir del mapping.
///
/// Es la pieza que mantiene al diseñador ajeno a SQL: hoy el esquema sale de
/// inferir un JSON de ejemplo; con un origen tabular sale de aquí. El
/// diseñador, el resolver y el sembrado del diccionario siguen funcionando
/// igual porque reciben exactamente el mismo tipo.
/// </summary>
public static class DataMappingSchemaFactory
{
    public static ReportDataSchema Create(
        DataMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return new ReportDataSchema
        {
            Fields = BuildFields(
                mapping.Root,
                prefix: string.Empty)
        };
    }

    private static List<ReportDataField> BuildFields(
        DataMappingNode node,
        string prefix)
    {
        var fields = new List<ReportDataField>();

        foreach (var field in node.Fields)
        {
            fields.Add(
                new ReportDataField
                {
                    Name = field.Name,
                    Path = Combine(prefix, field.Name),
                    Type = field.Type,
                    Nullable = field.Nullable
                });
        }

        foreach (var child in node.Children)
        {
            var path = Combine(prefix, child.Name);

            fields.Add(
                new ReportDataField
                {
                    Name = child.Name,
                    Path = path,

                    Type = child.Kind == DataMappingNodeKind.Collection
                        ? ReportDataType.Array
                        : ReportDataType.Object,

                    Nullable = false,

                    Children = BuildFields(child, path)
                });
        }

        return fields;
    }

    private static string Combine(
        string prefix,
        string name) =>
        string.IsNullOrWhiteSpace(prefix)
            ? name
            : $"{prefix}.{name}";
}
