namespace Renderset.Core.Definitions;

public sealed class ReportSectionDefinition
{
    public string Id { get; init; } = default!;

    public string Name { get; init; } = default!;

    public int Order { get; init; }

    public bool VisibleByDefault { get; init; } = true;

    public List<ReportFieldDefinition> Fields { get; init; } = [];

    public ReportTableDefinition? Table { get; init; }
}