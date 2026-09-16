using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportHeaderLine
{
    public required string Text { get; init; }

    public ReportHeaderLineStyle Style { get; init; }
}
