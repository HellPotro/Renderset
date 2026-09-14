using Renderset.Core.Definitions;

namespace Renderset.Core.Composer;

public sealed class SectionCompositionRequest
{
    public string? SectionId { get; set; }

    public string Name { get; set; } = "";

    public ReportSectionLayout Layout { get; set; }

    public List<string> SelectedFieldIds { get; set; } = [];
}