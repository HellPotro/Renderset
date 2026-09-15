using Renderset.Core.DataSources;
using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// Propone un mapping analizando una muestra de filas.
///
/// Detecta varios niveles: en una factura de automoción lo normal es
/// factura → albaranes → líneas, porque el JOIN arrastra la cabecera del
/// albarán repetida en cada línea igual que arrastra la de la factura en cada
/// albarán. Buscar un solo nivel dejaría el número y la fecha del albarán
/// dentro de cada línea, repetidos.
///
/// Es una sugerencia, no una verdad: con pocas filas de muestra cualquier
/// heurística se equivoca, así que la pantalla debe dejar corregirla.
/// </summary>
public static class DataMappingSuggester
{
    private const int MaxDepth = 3;

    public static DataMappingSuggestion Suggest(
        DataSetSchema schema,
        IReadOnlyList<object?[]> rows,
        string leafCollectionName = "lines")
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (rows.Count == 0)
        {
            return new DataMappingSuggestion
            {
                Mapping = FlatMapping(schema),
                Confidence = 0,
                Reason = "No hay filas de muestra."
            };
        }

        var allOrdinals =
            Enumerable
                .Range(0, schema.Columns.Count)
                .ToList();

        var rootCandidates =
            RankCandidates(
                schema,
                rows,
                Enumerable.Range(0, rows.Count).ToList(),
                allOrdinals,
                []);

        var best = rootCandidates.FirstOrDefault();

        if (best is null)
        {
            return new DataMappingSuggestion
            {
                Mapping = FlatMapping(schema),
                Candidates = rootCandidates,
                Confidence = 0,

                Reason =
                    "Ninguna columna agrupa las filas dejando otras " +
                    "constantes. Cada fila se tratará como un documento."
            };
        }

        var levels = new List<DataMappingLevel>();

        var root =
            BuildNode(
                schema,
                rows,
                rowIndexes: Enumerable.Range(0, rows.Count).ToList(),
                available: allOrdinals,
                usedKeys: [],
                name: string.Empty,
                path: string.Empty,
                kind: DataMappingNodeKind.Object,
                depth: 0,
                leafCollectionName: leafCollectionName,
                levels: levels);

        var depth = levels.Count - 1;

        return new DataMappingSuggestion
        {
            Mapping =
                new DataMapping
                {
                    Id = "suggested",
                    Name = "Mapping sugerido",
                    Root = root
                },

            Candidates = rootCandidates,
            Levels = levels,
            Confidence = best.Score,

            Reason =
                depth <= 0
                    ? $"'{best.Column}' agrupa {rows.Count} filas en " +
                      $"{best.GroupCount} documento(s)."
                    : $"'{best.Column}' agrupa {rows.Count} filas en " +
                      $"{best.GroupCount} documento(s), con {depth} " +
                      $"nivel(es) anidado(s) por debajo."
        };
    }


    /// <summary>
    /// Construye un nivel y, si las columnas que aún varían admiten otra
    /// agrupación, se llama a sí misma para el siguiente.
    /// </summary>
    private static DataMappingNode BuildNode(
        DataSetSchema schema,
        IReadOnlyList<object?[]> rows,
        List<int> rowIndexes,
        List<int> available,
        List<int> usedKeys,
        string name,
        string path,
        DataMappingNodeKind kind,
        int depth,
        string leafCollectionName,
        List<DataMappingLevel> levels)
    {
        var candidates =
            RankCandidates(
                schema,
                rows,
                rowIndexes,
                available,
                usedKeys);

        var best = candidates.FirstOrDefault();

        // Sin agrupación posible: todas las columnas que quedan son de este
        // nivel y no hay nada más abajo.
        if (best is null || depth >= MaxDepth)
        {
            var node = new DataMappingNode
            {
                Name = name,
                Kind = kind,

                Fields =
                    available
                        .Select(o => FieldFor(schema, o))
                        .ToList()
            };

            levels.Add(
                new DataMappingLevel
                {
                    Path = path,
                    Name = name,
                    Kind = kind,
                    KeyColumn = null,

                    Columns =
                        available
                            .Select(o => schema.Columns[o].Name)
                            .ToList()
                });

            return node;
        }

        var keyOrdinal = best.Ordinal;

        var constant = best.ConstantOrdinals;
        var varying = best.VaryingOrdinals;

        var current = new DataMappingNode
        {
            Name = name,
            Kind = kind,

            KeyColumns = [schema.Columns[keyOrdinal].Name],

            Fields =
                constant
                    .Select(o => FieldFor(schema, o))
                    .ToList()
        };

        levels.Add(
            new DataMappingLevel
            {
                Path = path,
                Name = name,
                Kind = kind,
                KeyColumn = schema.Columns[keyOrdinal].Name,

                Columns =
                    constant
                        .Select(o => schema.Columns[o].Name)
                        .ToList()
            });

        if (varying.Count == 0)
            return current;

        // Las filas del hijo son las mismas; lo que cambia es qué columnas
        // tiene disponibles y qué claves ya se han consumido.
        var childName =
            ChildName(
                schema,
                varying,
                depth,
                leafCollectionName);

        var childPath =
            string.IsNullOrEmpty(path)
                ? childName
                : $"{path}.{childName}";

        var child =
            BuildNode(
                schema,
                rows,
                rowIndexes,
                varying,
                [.. usedKeys, keyOrdinal],
                childName,
                childPath,
                DataMappingNodeKind.Collection,
                depth + 1,
                leafCollectionName,
                levels);

        current.Children.Add(child);

        return current;
    }


    /// <summary>
    /// Puntúa cada columna disponible como posible clave de este nivel. Una
    /// buena clave deja muchas columnas constantes dentro del grupo y al
    /// menos una que sigue variando: si no varía ninguna, agrupar no aporta
    /// nada porque no hay detalle debajo.
    /// </summary>
    private static List<DataMappingKeyCandidate> RankCandidates(
        DataSetSchema schema,
        IReadOnlyList<object?[]> rows,
        List<int> rowIndexes,
        List<int> available,
        IReadOnlyCollection<int> usedKeys)
    {
        var candidates = new List<DataMappingKeyCandidate>();

        foreach (var ordinal in available)
        {
            if (usedKeys.Contains(ordinal))
                continue;

            var groups =
                rowIndexes
                    .GroupBy(index => Key(rows[index][ordinal]))
                    .ToList();

            // Un valor distinto por fila no agrupa; un valor único no separa.
            if (groups.Count == rowIndexes.Count || groups.Count <= 1)
                continue;

            var constant = new List<int>();
            var varying = new List<int>();

            foreach (var other in available)
            {
                if (other == ordinal)
                {
                    constant.Add(other);
                    continue;
                }

                var isConstant =
                    groups.All(group =>
                        group
                            .Select(index => Key(rows[index][other]))
                            .Distinct()
                            .Count() <= 1);

                if (isConstant)
                    constant.Add(other);
                else
                    varying.Add(other);
            }

            if (varying.Count == 0)
                continue;

            candidates.Add(
                new DataMappingKeyCandidate
                {
                    Ordinal = ordinal,
                    Column = schema.Columns[ordinal].Name,
                    GroupCount = groups.Count,
                    ConstantOrdinals = constant,
                    VaryingOrdinals = varying,

                    ConstantColumns =
                        constant
                            .Select(o => schema.Columns[o].Name)
                            .ToList(),

                    VaryingColumns =
                        varying
                            .Select(o => schema.Columns[o].Name)
                            .ToList(),

                    // Se premia dejar muchas constantes y se penaliza
                    // fragmentar en muchos grupos: entre dos columnas igual
                    // de buenas gana la que produce grupos más gordos.
                    Score = constant.Count * 10 - groups.Count
                });
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .ToList();
    }


    /// <summary>
    /// Nombre por defecto del nivel, derivado del prefijo de sus columnas:
    /// Albaran_codigo y Albaran_fecha sugieren "albaran". El usuario lo
    /// renombra en la pantalla, esto es sólo para no llamarlos level1 y
    /// level2.
    /// </summary>
    private static string ChildName(
        DataSetSchema schema,
        IReadOnlyCollection<int> ordinals,
        int depth,
        string leafCollectionName)
    {
        var prefixes =
            ordinals
                .Select(o => schema.Columns[o].Name)
                .Select(PrefixOf)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Count())
                .ToList();

        var dominant = prefixes.FirstOrDefault();

        // Sólo se usa el prefijo si lo comparten al menos dos columnas: con
        // una sola es casualidad.
        if (dominant is not null && dominant.Count() >= 2)
            return ToCamelCase(dominant.Key);

        return depth == 0
            ? leafCollectionName
            : $"{leafCollectionName}{depth + 1}";
    }


    private static string PrefixOf(
        string columnName)
    {
        var separator = columnName.IndexOfAny(['_', '.', '-']);

        return separator > 0
            ? columnName[..separator]
            : string.Empty;
    }


    private static string Key(
        object? value) =>
        value?.ToString() ?? "\u0000";


    private static DataMappingField FieldFor(
        DataSetSchema schema,
        int ordinal)
    {
        var column = schema.Columns[ordinal];

        return new DataMappingField
        {
            Name = ToCamelCase(column.Name),
            SourceColumn = column.Name,
            Type = column.Type,
            Nullable = column.Nullable
        };
    }


    private static DataMapping FlatMapping(
        DataSetSchema schema) =>
        new()
        {
            Id = "suggested",
            Name = "Mapping sugerido",

            Root = new DataMappingNode
            {
                Fields =
                    Enumerable
                        .Range(0, schema.Columns.Count)
                        .Select(o => FieldFor(schema, o))
                        .ToList()
            }
        };


    /// <summary>
    /// "Total_linea" y "TOTAL LINEA" acaban en "totalLinea". Los nombres de
    /// columna de un ERP legacy no son presentables tal cual en el modelo.
    /// </summary>
    public static string ToCamelCase(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts =
            value.Split(['_', ' ', '-', '.'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return value;

        var result = parts[0].ToLowerInvariant();

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i].ToLowerInvariant();

            result += char.ToUpperInvariant(part[0]) + part[1..];
        }

        return result;
    }
}
