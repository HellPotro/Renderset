using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportHeaderColumn
{
    public required string Id { get; init; }

    public ReportHorizontalAlignment Align { get; init; } =
        ReportHorizontalAlignment.Left;

    public int Width { get; init; } = 50;

    public List<ResolvedReportHeaderLine> Lines { get; init; } = [];
}
