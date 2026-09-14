namespace Renderset.Core.Configurations;

public sealed class ReportFieldConfiguration
{
    public string FieldId { get; set; } = default!;

    public bool? Visible { get; set; }

    public int? Order { get; set; }

    public string? LabelKey { get; set; }
}