using Renderset.Core.Reports;

namespace Renderset.Core.DataSources;

/// <summary>
/// Una columna del resultado plano, sea cual sea el origen.
/// </summary>
public sealed class DataSetColumn
{
    public required string Name { get; init; }

    public required int Ordinal { get; init; }

    public ReportDataType Type { get; init; } = ReportDataType.String;

    public bool Nullable { get; init; } = true;

    /// <summary>
    /// Tipo original del proveedor (System.Data, tipo de celda de Excel...).
    /// Sólo informativo: el motor nunca lo mira.
    /// </summary>
    public string? NativeType { get; init; }
}
