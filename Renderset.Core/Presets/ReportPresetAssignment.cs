namespace Renderset.Core.Presets;

public sealed class ReportPresetAssignment
{
    public long? Id { get; set; }

    public required string ReportId { get; set; }

    public string? ContextType { get; set; }

    public string? ContextKey { get; set; }

    public required string PresetId { get; set; }
}