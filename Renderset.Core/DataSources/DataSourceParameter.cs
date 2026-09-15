using Renderset.Core.Reports;

namespace Renderset.Core.DataSources;

/// <summary>
/// Parámetro de entrada del origen. En SQL se enlaza como parámetro de
/// comando, nunca por concatenación de cadenas.
/// </summary>
public sealed class DataSourceParameter
{
    public required string Name { get; set; }

    public ReportDataType Type { get; set; } = ReportDataType.String;

    public bool Required { get; set; } = true;

    public string? DefaultValue { get; set; }

    public string? Description { get; set; }
}
