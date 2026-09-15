using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Renderset.Core.DataSources;
using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// Único punto del sistema que sabe convertir un resultado plano en el modelo
/// jerárquico. Ni el diseñador, ni el resolver, ni el motor de PDF saben que
/// esta clase existe.
///
/// Produce JsonElement a propósito, y no un modelo propio: todo el render
/// actual está tipado sobre JsonElement (IReportDataAccessor, los componentes
/// de Rendering, SampleData). Cambiar esa frontera por pureza costaría tocar
/// medio proyecto para ahorrar unas asignaciones que no se notan al lado de
/// generar un PDF.
/// </summary>
public sealed class HierarchyBuilder
    : IHierarchyBuilder
{
    public async IAsyncEnumerable<JsonElement> BuildAsync(
        IDataSetReader reader,
        DataMapping mapping,
        HierarchyBuilderOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(mapping);

        var settings = options ?? new HierarchyBuilderOptions();

        var plan = MappingPlan.Create(
            mapping.Root,
            reader.Schema);

        if (settings.AssumeOrderedByKey)
        {
            await foreach (var document in BuildStreamingAsync(
                               reader,
                               plan,
                               settings,
                               cancellationToken))
            {
                yield return document;
            }

            yield break;
        }

        foreach (var document in await BuildBufferedAsync(
                     reader,
                     plan,
                     settings,
                     cancellationToken))
        {
            yield return document;
        }
    }

    // ------------------------------------------------------------ streaming

    private static async IAsyncEnumerable<JsonElement> BuildStreamingAsync(
        IDataSetReader reader,
        MappingPlan plan,
        HierarchyBuilderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = new List<object?[]>();
        string? currentKey = null;
        var rows = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (options.MaxRows > 0 && ++rows > options.MaxRows)
                break;

            var row = ReadRow(reader);

            var key = plan.Root.KeyOf(row);

            if (currentKey is not null &&
                !string.Equals(key, currentKey, StringComparison.Ordinal))
            {
                yield return BuildDocument(
                    plan.Root,
                    current,
                    options);

                current = [];
            }

            currentKey = key;
            current.Add(row);
        }

        if (current.Count > 0)
        {
            yield return BuildDocument(
                plan.Root,
                current,
                options);
        }
    }

    // ------------------------------------------------------------- buffered

    private static async Task<List<JsonElement>> BuildBufferedAsync(
        IDataSetReader reader,
        MappingPlan plan,
        HierarchyBuilderOptions options,
        CancellationToken cancellationToken)
    {
        // Se conserva el orden de primera aparición: si la consulta no está
        // ordenada, al menos los documentos salen en el orden en que el
        // origen los mencionó por primera vez.
        var groups = new Dictionary<string, List<object?[]>>(
            StringComparer.Ordinal);

        var order = new List<string>();
        var rows = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (options.MaxRows > 0 && ++rows > options.MaxRows)
                break;

            var row = ReadRow(reader);
            var key = plan.Root.KeyOf(row);

            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = [];
                groups[key] = bucket;
                order.Add(key);
            }

            bucket.Add(row);
        }

        return order
            .Select(key => BuildDocument(
                plan.Root,
                groups[key],
                options))
            .ToList();
    }

    // ---------------------------------------------------------- construcción

    private static object?[] ReadRow(
        IDataSetReader reader)
    {
        var values = new object?[reader.Schema.Columns.Count];

        for (var i = 0; i < values.Length; i++)
            values[i] = reader.GetValue(i);

        return values;
    }

    private static JsonElement BuildDocument(
        NodePlan root,
        IReadOnlyList<object?[]> rows,
        HierarchyBuilderOptions options)
    {
        var node = BuildObject(root, rows, options);

        return JsonSerializer.SerializeToElement(node);
    }

    private static JsonObject BuildObject(
        NodePlan plan,
        IReadOnlyList<object?[]> rows,
        HierarchyBuilderOptions options)
    {
        var result = new JsonObject();

        var first = rows[0];

        foreach (var field in plan.Fields)
            result[field.Name] = ToJsonValue(field, first);

        foreach (var child in plan.Children)
        {
            if (child.Kind == DataMappingNodeKind.Object)
            {
                result[child.Name] =
                    BuildObject(child, rows, options);

                continue;
            }

            result[child.Name] =
                BuildCollection(child, rows, options);
        }

        return result;
    }

    private static JsonArray BuildCollection(
        NodePlan plan,
        IReadOnlyList<object?[]> rows,
        HierarchyBuilderOptions options)
    {
        var array = new JsonArray();

        var relevant = rows;

        if (options.SkipEmptyChildRows && plan.OwnedOrdinals.Length > 0)
        {
            relevant = rows
                .Where(row => plan.OwnedOrdinals.Any(o => row[o] is not null))
                .ToList();
        }

        if (relevant.Count == 0)
            return array;

        // Sin claves, cada fila es un elemento. Es lo correcto cuando el
        // detalle no tiene identificador natural.
        if (plan.KeyOrdinals.Length == 0)
        {
            foreach (var row in relevant)
                array.Add(BuildObject(plan, [row], options));

            return array;
        }

        var groups = new Dictionary<string, List<object?[]>>(
            StringComparer.Ordinal);

        var order = new List<string>();

        foreach (var row in relevant)
        {
            var key = plan.KeyOf(row);

            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = [];
                groups[key] = bucket;
                order.Add(key);
            }

            bucket.Add(row);
        }

        foreach (var key in order)
            array.Add(BuildObject(plan, groups[key], options));

        return array;
    }

    private static JsonNode? ToJsonValue(
        FieldPlan field,
        object?[] row)
    {
        if (field.Ordinal < 0)
        {
            return field.ConstantValue is null
                ? null
                : JsonValue.Create(field.ConstantValue);
        }

        var value = row[field.Ordinal];

        if (value is null)
            return null;

        return field.Type switch
        {
            ReportDataType.Number =>
                JsonValue.Create(Convert.ToDecimal(value)),

            ReportDataType.Boolean =>
                JsonValue.Create(Convert.ToBoolean(value)),

            ReportDataType.Date =>
                JsonValue.Create(
                    Convert.ToDateTime(value).ToString("yyyy-MM-dd")),

            ReportDataType.DateTime =>
                JsonValue.Create(
                    Convert.ToDateTime(value).ToString("s")),

            _ =>
                JsonValue.Create(value.ToString())
        };
    }

    // ------------------------------------------------------------- planning

    /// <summary>
    /// El mapping trabaja con nombres de columna; leer filas por nombre en
    /// cada acceso es caro. El plan resuelve los nombres a posiciones una
    /// sola vez, al empezar.
    /// </summary>
    private sealed class MappingPlan
    {
        public required NodePlan Root { get; init; }

        public static MappingPlan Create(
            DataMappingNode root,
            DataSetSchema schema) =>
            new()
            {
                Root = NodePlan.Create(root, schema)
            };
    }

    private sealed class NodePlan
    {
        public required string Name { get; init; }

        public required DataMappingNodeKind Kind { get; init; }

        public required int[] KeyOrdinals { get; init; }

        public required int[] OwnedOrdinals { get; init; }

        public required List<FieldPlan> Fields { get; init; }

        public required List<NodePlan> Children { get; init; }

        public string KeyOf(
            object?[] row)
        {
            if (KeyOrdinals.Length == 0)
                return string.Empty;

            return string.Join(
                '\u001f',
                KeyOrdinals.Select(o => row[o]?.ToString() ?? "\u0000"));
        }

        public static NodePlan Create(
            DataMappingNode node,
            DataSetSchema schema)
        {
            var keyOrdinals =
                node.KeyColumns
                    .Select(schema.IndexOf)
                    .Where(x => x >= 0)
                    .ToArray();

            var ownedOrdinals =
                node.AllSourceColumns()
                    .Select(schema.IndexOf)
                    .Where(x => x >= 0)
                    .Distinct()
                    .ToArray();

            return new NodePlan
            {
                Name = node.Name,
                Kind = node.Kind,
                KeyOrdinals = keyOrdinals,
                OwnedOrdinals = ownedOrdinals,

                Fields =
                    node.Fields
                        .Select(field => new FieldPlan
                        {
                            Name = field.Name,
                            Type = field.Type,
                            ConstantValue = field.ConstantValue,

                            Ordinal =
                                string.IsNullOrWhiteSpace(field.SourceColumn)
                                    ? -1
                                    : schema.IndexOf(field.SourceColumn)
                        })
                        .ToList(),

                Children =
                    node.Children
                        .Select(child => Create(child, schema))
                        .ToList()
            };
        }
    }

    private sealed class FieldPlan
    {
        public required string Name { get; init; }

        public required int Ordinal { get; init; }

        public required ReportDataType Type { get; init; }

        public string? ConstantValue { get; init; }
    }
}
