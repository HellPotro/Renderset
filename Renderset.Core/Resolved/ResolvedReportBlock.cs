using Renderset.Core.Blocks;
using Renderset.Core.Configurations;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportBlock
{
    public required string Id { get; init; }

    public string? Name { get; init; }

    public ReportBlockType Type { get; init; }

    public ReportTextBlockConfiguration? Text { get; init; }
}
