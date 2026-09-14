namespace Renderset.Core.Definitions;

public sealed class ReportFieldDefinition
{
    public string Id { get; init; } = default!;

    public string Label { get; init; } = default!;

    public string DataPath { get; init; } = default!;

    public int Order { get; init; }

    public bool VisibleByDefault { get; init; } = true;

    public ReportFieldType Type { get; init; } = ReportFieldType.Text;
}