using Renderset.Core.Persistence;

namespace Renderset.Core.Presets;

public sealed class ReportPresetProvider
    : IReportPresetProvider
{
    private readonly IReportPresetRepository _presetRepository;
    private readonly IReportPresetAssignmentRepository _assignmentRepository;

    public ReportPresetProvider(
        IReportPresetRepository presetRepository,
        IReportPresetAssignmentRepository assignmentRepository)
    {
        _presetRepository = presetRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType = null,
        string? contextKey = null,
        CancellationToken cancellationToken = default)
    {
        ReportPresetAssignment? assignment = null;

        if (!string.IsNullOrWhiteSpace(contextType) &&
            !string.IsNullOrWhiteSpace(contextKey))
        {
            assignment =
                await _assignmentRepository.GetAsync(
                    tenantId,
                    reportId,
                    contextType,
                    contextKey,
                    cancellationToken);
        }

        assignment ??=
            await _assignmentRepository.GetDefaultAsync(
                tenantId,
                reportId,
                cancellationToken);

        if (assignment is null)
            return null;

        return await _presetRepository.GetByIdAsync(
            tenantId,
            assignment.PresetId,
            cancellationToken);
    }
}