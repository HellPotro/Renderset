namespace Renderset.Core.Definitions;

public sealed class ReportTableDefinition
{
    public string Id { get; init; } = default!;

    public string Name { get; init; } = default!;

    public string DataPath { get; init; } = default!;

    public List<ReportColumnDefinition> Columns { get; init; } = [];
}