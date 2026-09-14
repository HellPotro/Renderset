using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportSection
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Order { get; init; }

    public bool Visible { get; init; }

    public ReportSectionLayout Layout { get; set; }

    public List<ResolvedReportField> Fields { get; init; } = [];

    public ResolvedReportTable? Table { get; init; }

    public ResolvedReportField? GetField(string id)
        => Fields.FirstOrDefault(
            x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase));
}