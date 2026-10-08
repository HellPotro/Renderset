using Renderset.Core.Definitions;

namespace Renderset.Core.Composer;

public sealed class SectionCompositionRequest
{
    public string? SectionId { get; set; }

    public string Name { get; set; } = "";

    public ReportSectionLayout Layout { get; set; }

    public int Columns { get; set; } = ReportSectionGrid.DefaultColumns;

    public List<string> SelectedFieldIds { get; set; } = [];
}