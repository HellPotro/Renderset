namespace Renderset.Core.Reports;

public sealed class ReportDataField
{
    public required string Name { get; set; }

    public required string Path { get; set; }

    public ReportDataType Type { get; set; }

    public bool Nullable { get; set; }

    public List<ReportDataField> Children { get; set; } = [];
}