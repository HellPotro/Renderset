namespace Renderset.Core.Definitions;

public sealed class ReportBodyItemConfiguration
{
    public required string Id { get; set; }

    public ReportBodyItemType Type { get; set; }

    public int Order { get; set; }

    public string? SectionId { get; set; }

    public string? BlockId { get; set; }
}