namespace Renderset.Core.Resolved;

public sealed class ResolvedReportDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Version { get; init; }

    public ResolvedReportHeader? Header { get; set; }

    public List<ResolvedReportSection> Sections { get; init; } = [];

    public ResolvedReportFooter? Footer { get; set; }

    public ResolvedReportSection? GetSection(string id)
        => Sections.FirstOrDefault(
            x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase));
}