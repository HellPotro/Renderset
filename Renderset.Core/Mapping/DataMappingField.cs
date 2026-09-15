using Renderset.Core.Reports;

namespace Renderset.Core.Mapping;

public sealed class DataMappingField
{
    /// <summary>
    /// Nombre en el modelo jerárquico. Es lo que verá el diseñador y lo que
    /// acabará en la clave de diccionario.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Columna del origen. Nulo si el valor es constante.
    /// </summary>
    public string? SourceColumn { get; set; }

    /// <summary>
    /// Valor fijo, para cuando el dato no está en la consulta.
    /// </summary>
    public string? ConstantValue { get; set; }

    public ReportDataType Type { get; set; } = ReportDataType.String;

    public bool Nullable { get; set; } = true;
}
