using System.Globalization;
using System.Text.Json;
using Renderset.Core.DataSources;
using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// El camino inverso de <see cref="HierarchyBuilder"/>: a partir de un
/// documento ya construido y del mapping que lo construyó, vuelve a las filas
/// planas de la consulta.
///
/// Sirve para reorganizar un report ya creado sin volver a pegar el SELECT:
/// los datos de ejemplo del report se aplanan, el asistente se abre con esas
/// filas y con los campos donde estaban, y se pueden mover de nivel.
///
/// Una fila por elemento del nivel más profundo; un elemento sin hijos da
/// una fila con lo de arriba, igual que un LEFT JOIN. Si la clave de un nivel
/// no está entre sus campos (no viaja en el JSON), se inventa un valor por
/// elemento para que vuelvan a agruparse igual.
/// </summary>
public static class DataMappingFlattener
{
    public static TabularPayload Flatten(
        DataMapping mapping,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var columns = new List<TabularPayloadColumn>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        CollectColumns(mapping.Root, columns, index);

        var payload = new TabularPayload { Columns = columns };

        if (document.ValueKind != JsonValueKind.Object || columns.Count == 0)
            return payload;

        var counter = 0;

        foreach (var row in Rows(mapping.Root, document, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), ref counter))
        {
            var values = new List<object?>(columns.Count);

            foreach (var column in columns)
                values.Add(row.TryGetValue(column.Name, out var value) ? value : null);

            payload.Rows.Add(values);
        }

        return payload;
    }

    private static void CollectColumns(
        DataMappingNode node,
        List<TabularPayloadColumn> columns,
        Dictionary<string, int> index)
    {
        void Add(string? name, ReportDataType type)
        {
            if (string.IsNullOrWhiteSpace(name) || index.ContainsKey(name))
                return;

            index[name] = columns.Count;
            columns.Add(new TabularPayloadColumn { Name = name, Type = type });
        }

        foreach (var key in node.KeyColumns)
        {
            var keyField = node.Fields.FirstOrDefault(x =>
                string.Equals(x.SourceColumn, key, StringComparison.OrdinalIgnoreCase));

            Add(key, keyField?.Type ?? ReportDataType.String);
        }

        foreach (var field in node.Fields)
            Add(field.SourceColumn, field.Type);

        foreach (var child in node.Children)
            CollectColumns(child, columns, index);
    }

    /// <summary>
    /// Filas de un elemento: sus valores más los de arriba, multiplicados por
    /// los elementos de cada nivel de debajo.
    /// </summary>
    private static List<Dictionary<string, object?>> Rows(
        DataMappingNode node,
        JsonElement element,
        Dictionary<string, object?> inherited,
        ref int counter)
    {
        var own = new Dictionary<string, object?>(inherited, StringComparer.OrdinalIgnoreCase);

        foreach (var field in node.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.SourceColumn))
                continue;

            own[field.SourceColumn] =
                TryGetProperty(element, field.Name, out var value)
                    ? ToValue(value)
                    : null;
        }

        // Clave que no viaja en el JSON: un valor por elemento basta para
        // que el asistente vuelva a separarlos.
        foreach (var key in node.KeyColumns)
        {
            if (!own.TryGetValue(key, out var existing) || existing is null)
                own[key] = (++counter).ToString(CultureInfo.InvariantCulture);
        }

        var rows = new List<Dictionary<string, object?>>();

        foreach (var child in node.Children)
        {
            if (!TryGetProperty(element, child.Name, out var value))
                continue;

            List<JsonElement> items =
                value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().ToList()
                    : value.ValueKind == JsonValueKind.Object
                        ? [value]
                        : [];

            foreach (var item in items)
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                rows.AddRange(Rows(child, item, own, ref counter));
            }
        }

        // Sin hijos (o con las colecciones vacías) el elemento es una fila.
        if (rows.Count == 0)
            rows.Add(own);

        return rows;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out value))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static object? ToValue(
        JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetDecimal(out var number) ? number : value.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText()
        };
}
