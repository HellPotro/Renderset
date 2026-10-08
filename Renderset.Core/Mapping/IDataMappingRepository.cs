namespace Renderset.Core.Mapping;

public interface IDataMappingRepository
{
    Task<IReadOnlyCollection<DataMapping>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<DataMapping?> GetByIdAsync(
        string tenantId,
        string mappingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mapping asignado al report (<see cref="DataMapping.ReportId"/>).
    /// </summary>
    Task<DataMapping?> GetForReportAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o actualiza. Si trae ReportId, se lo quita a cualquier otro
    /// mapping que lo tuviera.
    /// </summary>
    Task SaveAsync(
        string tenantId,
        DataMapping mapping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica.
    /// </summary>
    Task<bool> DeleteAsync(
        string tenantId,
        string mappingId,
        CancellationToken cancellationToken = default);
}
