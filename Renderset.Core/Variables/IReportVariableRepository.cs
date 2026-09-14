namespace Renderset.Core.Variables;

public interface IReportVariableRepository
{
    Task<IReadOnlyCollection<ReportVariable>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ReportVariable?> GetAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        ReportVariable variable,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default);
}