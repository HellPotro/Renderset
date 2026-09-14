using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportColumn
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string DataPath { get; init; }

    public int Order { get; init; }

    public bool Visible { get; init; }

    public decimal? Width { get; init; }

    public ReportFieldType Type { get; init; }
}