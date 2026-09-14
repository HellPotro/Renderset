using Renderset.Core.Persistence;

namespace Renderset.Core.Presets;

public interface IReportPresetRepository
{
    Task<ReportPreset?> GetByIdAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportPreset>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        ReportPreset preset,
        CancellationToken cancellationToken = default);
}