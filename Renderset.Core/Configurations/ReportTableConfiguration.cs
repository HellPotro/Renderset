namespace Renderset.Core.Configurations;

public sealed class ReportTableConfiguration
{
    public string TableId { get; set; } = default!;

    public List<ReportColumnConfiguration> Columns { get; set; } = [];
}