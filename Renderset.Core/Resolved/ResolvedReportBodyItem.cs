using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportBodyItem
{
    public required string Id { get; init; }

    public ReportBodyItemType Type { get; init; }

    public int Order { get; init; }

    public ResolvedReportSection? Section { get; init; }

    public ResolvedReportBlock? Block { get; init; }
}
