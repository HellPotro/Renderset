namespace Renderset.Core.Reports;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Report>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        Report report,
        CancellationToken cancellationToken = default);
}