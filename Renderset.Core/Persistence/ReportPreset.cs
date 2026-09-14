using Renderset.Core.Configurations;
using Renderset.Core.Themes;

namespace Renderset.Core.Persistence;

public sealed class ReportPreset
{
    public string Id { get; set; } = default!;

    public int Version { get; set; } = 1;

    public required ReportConfiguration Configuration { get; set; }

    public required ReportTheme Theme { get; set; }
}