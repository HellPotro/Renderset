namespace Renderset.Core.Translations;

public sealed class TranslateMissingReportResourcesResult
{
    public required string Scope { get; set; }

    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    public required string Provider { get; set; }

    public int Translated { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }
}
