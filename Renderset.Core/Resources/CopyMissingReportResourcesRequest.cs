namespace Renderset.Core.Resources;

public sealed class CopyMissingReportResourcesRequest
{
    public required string Scope { get; set; }

    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    public bool CopyValue { get; set; } = true;
}