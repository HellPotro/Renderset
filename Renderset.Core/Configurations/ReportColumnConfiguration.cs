namespace Renderset.Core.Configurations;

public sealed class ReportColumnConfiguration
{
    public string ColumnId { get; set; } = default!;

    public bool? Visible { get; set; }

    public int? Order { get; set; }

    public decimal? Width { get; set; }

    public string? LabelOverride { get; set; }
}