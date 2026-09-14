namespace Renderset.Core.Blocks;

public sealed class ReportBlock
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public ReportBlockType Type { get; set; }

    public string ConfigurationJson { get; set; } = "{}";
}
