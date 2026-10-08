using System.Text.RegularExpressions;
using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// Validación de un mapping y comparación con el esquema del report.
/// </summary>
public static partial class DataMappingRules
{
    public const int MaxIdLength = 100;

    public const int MaxNameLength = 200;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Errores que impiden guardar: el mapping no podría construir nada o
    /// construiría un JSON ambiguo (dos campos con el mismo nombre).
    /// </summary>
    public static IReadOnlyList<string> Validate(
        DataMapping? mapping)
    {
        var errors = new List<string>();

        if (mapping is null)
        {
            errors.Add("Falta el mapping.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(mapping.Id) ||
            mapping.Id.Length > MaxIdLength ||
            !IdPattern().IsMatch(mapping.Id))
        {
            errors.Add("El identificador sólo admite letras, números, '.', '_' y '-' (máx. 100).");
        }

        if (string.IsNullOrWhiteSpace(mapping.Name))
            errors.Add("El nombre es obligatorio.");
        else if (mapping.Name.Length > MaxNameLength)
            errors.Add($"El nombre admite como mucho {MaxNameLength} caracteres.");

        if (mapping.Root is null)
        {
            errors.Add("Falta el nivel del documento.");
            return errors;
        }

        if (mapping.Root.Kind != DataMappingNodeKind.Object)
            errors.Add("El nivel del documento tiene que ser un objeto, no una colección.");

        ValidateNode(mapping.Root, "documento", errors);

        if (!mapping.Root.AllSourceColumns().Any())
            errors.Add("El mapping no usa ninguna columna.");

        return errors;
    }

    private static void ValidateNode(
        DataMappingNode node,
        string label,
        List<string> errors)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in node.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add($"Hay un campo sin nombre en {label}.");
                continue;
            }

            if (!names.Add(field.Name))
                errors.Add($"El campo '{field.Name}' está repetido en {label}.");

            if (string.IsNullOrWhiteSpace(field.SourceColumn) &&
                field.ConstantValue is null)
            {
                errors.Add($"El campo '{field.Name}' de {label} no tiene columna ni valor fijo.");
            }
        }

        foreach (var child in node.Children)
        {
            if (string.IsNullOrWhiteSpace(child.Name))
            {
                errors.Add($"Hay un nivel sin nombre dentro de {label}.");
                continue;
            }

            if (!names.Add(child.Name))
                errors.Add($"'{child.Name}' está repetido en {label}.");

            ValidateNode(child, $"'{child.Name}'", errors);
        }
    }

    /// <summary>
    /// Columnas que el mapping necesita y no vienen en las filas.
    /// </summary>
    public static IReadOnlyList<string> MissingColumns(
        DataMapping mapping,
        IEnumerable<string> availableColumns)
    {
        var available =
            new HashSet<string>(availableColumns, StringComparer.OrdinalIgnoreCase);

        return mapping.Root
            .AllSourceColumns()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(column => !available.Contains(column))
            .ToList();
    }

    /// <summary>
    /// Compara lo que produce el mapping con lo que espera el report. Es lo
    /// que avisa, al configurar, de que "descripcion" no va a llegar porque
    /// el mapping lo llama "descripcionEsp".
    /// </summary>
    public static IReadOnlyList<DataMappingCompatibilityIssue> CompareWithReport(
        DataMapping mapping,
        ReportDataSchema reportSchema)
    {
        var produced = Flatten(DataMappingSchemaFactory.Create(mapping).Fields);
        var expected = Flatten(reportSchema.Fields);

        var issues = new List<DataMappingCompatibilityIssue>();

        foreach (var (path, field) in expected)
        {
            if (!produced.TryGetValue(path, out var actual))
            {
                issues.Add(new DataMappingCompatibilityIssue
                {
                    Kind = DataMappingCompatibilityKind.MissingInMapping,
                    Path = path,
                    Message = IsContainer(field.Type)
                        ? $"El report espera '{path}' y el mapping no lo genera: esa sección saldrá vacía."
                        : $"El report usa '{path}' y el mapping no lo genera: saldrá vacío."
                });

                continue;
            }

            if (IsContainer(field.Type) != IsContainer(actual.Type))
            {
                issues.Add(new DataMappingCompatibilityIssue
                {
                    Kind = DataMappingCompatibilityKind.ShapeMismatch,
                    Path = path,
                    Message = IsContainer(field.Type)
                        ? $"En el report '{path}' es una lista y el mapping lo genera como campo."
                        : $"En el report '{path}' es un campo y el mapping lo genera como lista."
                });
            }
        }

        foreach (var (path, _) in produced)
        {
            if (!expected.ContainsKey(path))
            {
                issues.Add(new DataMappingCompatibilityIssue
                {
                    Kind = DataMappingCompatibilityKind.NotInReport,
                    Path = path,
                    Message = $"El mapping genera '{path}', que el report todavía no usa."
                });
            }
        }

        return issues;
    }

    private static bool IsContainer(ReportDataType type) =>
        type is ReportDataType.Array or ReportDataType.Object;

    private static Dictionary<string, ReportDataField> Flatten(
        IEnumerable<ReportDataField> fields)
    {
        var result =
            new Dictionary<string, ReportDataField>(StringComparer.OrdinalIgnoreCase);

        void Walk(IEnumerable<ReportDataField> items)
        {
            foreach (var field in items)
            {
                result.TryAdd(field.Path, field);
                Walk(field.Children);
            }
        }

        Walk(fields);

        return result;
    }
}


public enum DataMappingCompatibilityKind
{
    /// <summary>
    /// El report lo usa y el mapping no lo genera. Es lo grave.
    /// </summary>
    MissingInMapping,

    /// <summary>
    /// Lista en un lado y campo en el otro.
    /// </summary>
    ShapeMismatch,

    /// <summary>
    /// El mapping lo genera y el report no lo usa. Sólo informativo.
    /// </summary>
    NotInReport
}


public sealed class DataMappingCompatibilityIssue
{
    public required DataMappingCompatibilityKind Kind { get; init; }

    public required string Path { get; init; }

    public required string Message { get; init; }

    public bool IsWarning =>
        Kind != DataMappingCompatibilityKind.NotInReport;
}
