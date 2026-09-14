using Renderset.Core.Persistence;

namespace Renderset.Core.Presets;

public interface IReportPresetProvider
{
    Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType = null,
        string? contextKey = null,
        CancellationToken cancellationToken = default);
}