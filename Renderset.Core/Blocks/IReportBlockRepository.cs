namespace Renderset.Core.Blocks;

public interface IReportBlockRepository
{
    Task<ReportBlock?> GetByIdAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportBlock>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportBlock>> GetByTypeAsync(
        string tenantId,
        ReportBlockType type,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        ReportBlock block,
        CancellationToken cancellationToken = default);
}