namespace Renderset.Core.Resources;

public sealed class CopyMissingReportResourcesResult
{
    public required string Scope { get; set; }

    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    public int Created { get; set; }

    public int Skipped { get; set; }
}