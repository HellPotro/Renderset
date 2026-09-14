namespace Renderset.Core.Presets;

public interface IReportPresetAssignmentRepository
{
    Task<ReportPresetAssignment?> GetAsync(
        string tenantId,
        string reportId,
        string contextType,
        string contextKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportPresetAssignment>> GetAllAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    Task<ReportPresetAssignment?> GetDefaultAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        ReportPresetAssignment assignment,
        CancellationToken cancellationToken = default);
}