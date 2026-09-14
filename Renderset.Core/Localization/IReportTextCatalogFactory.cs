namespace Renderset.Core.Localization;

public interface IReportTextCatalogFactory
{
    Task<ReportTextCatalog> CreateAsync(
        string tenantId,
        string reportId,
        string? presetId,
        string? culture,
        CancellationToken cancellationToken = default);
}
