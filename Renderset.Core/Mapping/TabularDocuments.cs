using System.Text.Json;
using Renderset.Core.DataSources;

namespace Renderset.Core.Mapping;

/// <summary>
/// Filas planas recibidas por API → documentos jerárquicos, con el mapping
/// guardado. Lo usan el render y la prueba de mappings, así que los dos
/// construyen exactamente lo mismo.
/// </summary>
public static class TabularDocuments
{
    /// <summary>
    /// Límite de filas por petición. Un packing list o una factura son
    /// cientos; esto sólo frena un envío equivocado (la tabla entera).
    /// </summary>
    public const int MaxRows = 50_000;

    public static async Task<IReadOnlyList<JsonElement>> BuildAsync(
        TabularPayload rows,
        DataMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(mapping);

        var documents = new List<JsonElement>();

        await using var reader =
            new TabularPayloadDataSet(rows);

        await foreach (var document in new HierarchyBuilder().BuildAsync(
                           reader,
                           mapping,
                           new HierarchyBuilderOptions
                           {
                               MaxRows = MaxRows
                           },
                           cancellationToken))
        {
            documents.Add(document);
        }

        return documents;
    }

    /// <summary>
    /// Problemas de forma de las filas que conviene contar antes de
    /// construir: el ERP tiene que saber qué columna falta, no recibir un
    /// documento con huecos.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        TabularPayload rows,
        DataMapping mapping)
    {
        var errors = new List<string>();

        if (rows.Columns.Count == 0)
        {
            errors.Add("'rows.columns' está vacío.");
            return errors;
        }

        var duplicated =
            rows.Columns
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();

        if (duplicated.Count > 0)
            errors.Add($"Columnas repetidas: {string.Join(", ", duplicated)}. Usa un alias distinto en el SELECT.");

        if (rows.Rows.Count == 0)
            errors.Add("'rows.rows' no trae ninguna fila.");

        if (rows.Rows.Count > MaxRows)
            errors.Add($"Como mucho {MaxRows} filas por petición; han llegado {rows.Rows.Count}.");

        var width = rows.Columns.Count;

        var badRow =
            rows.Rows.FindIndex(x => x is null || x.Count != width);

        if (badRow >= 0)
        {
            errors.Add(
                $"La fila {badRow} trae {rows.Rows[badRow]?.Count ?? 0} valores y hay {width} columnas. " +
                "Cada fila lleva un valor por columna, en el mismo orden.");
        }

        var missing =
            DataMappingRules.MissingColumns(
                mapping,
                rows.Columns.Select(x => x.Name));

        if (missing.Count > 0)
        {
            errors.Add(
                $"Faltan columnas que usa el mapping '{mapping.Id}': {string.Join(", ", missing)}.");
        }

        return errors;
    }
}
