using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

/// <summary>
/// Un valor de una columna no se puede convertir al tipo del campo del
/// mapping ("abc" en una columna numérica). Lleva columna, campo y valor
/// para que quien integra sepa qué fila y qué dato corregir.
/// </summary>
public sealed class DataMappingValueException
    : Exception
{
    public DataMappingValueException(
        string field,
        string? sourceColumn,
        ReportDataType expectedType,
        object? value,
        Exception innerException)
        : base(
            $"La columna '{sourceColumn ?? field}' trae '{value}', que no se puede leer como {expectedType} (campo '{field}').",
            innerException)
    {
        Field = field;
        SourceColumn = sourceColumn;
        ExpectedType = expectedType;
        Value = value?.ToString();
    }

    public string Field { get; }

    public string? SourceColumn { get; }

    public ReportDataType ExpectedType { get; }

    public string? Value { get; }
}
