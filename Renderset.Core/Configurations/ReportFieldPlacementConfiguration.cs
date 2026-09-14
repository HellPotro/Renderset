namespace Renderset.Core.Configurations;

public sealed class ReportFieldPlacementConfiguration
{
    public required string FieldId { get; set; }

    public required string SectionId { get; set; }

    public int Order { get; set; }
}