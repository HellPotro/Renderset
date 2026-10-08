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

    /// <summary>
    /// Guarda varias asignaciones de una vez (importación de CSV). Mismas
    /// reglas que <see cref="SaveAsync"/>: un contexto que ya existe cambia
    /// de preset.
    /// </summary>
    Task<int> SaveManyAsync(
        string tenantId,
        IReadOnlyCollection<ReportPresetAssignment> assignments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita una asignación. Falso si no existía en el tenant.
    /// </summary>
    Task<bool> DeleteAsync(
        string tenantId,
        long assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita todas las asignaciones que apuntan a un preset (al borrarlo).
    /// </summary>
    Task<int> DeleteByPresetAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default);
}