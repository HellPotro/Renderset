using Renderset.Core.Resources;

namespace Renderset.Core.Localization;

public interface IReportResourceRepository
{
    Task<IReadOnlyCollection<ReportResource>> GetByScopeAsync(
        string tenantId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los recursos de varios ámbitos en una sola consulta. Lo usa
    /// la fábrica del catálogo para no hacer tres viajes por render.
    /// </summary>
    Task<IReadOnlyCollection<ReportResource>> GetByScopesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportResourceCoverage>> GetCoverageAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        ReportResource resource,
        CancellationToken cancellationToken = default);

    Task SaveManyAsync(
        string tenantId,
        IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta las claves que aún no existen sin pisar las que ya tienen
    /// valor. Se usa al sembrar un report recién inferido.
    /// </summary>
    Task SeedMissingAsync(
        string tenantId,
        IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string tenantId,
        string scope,
        string key,
        CancellationToken cancellationToken = default);

    Task<CopyMissingReportResourcesResult> CopyMissingAsync(
        string tenantId,
        CopyMissingReportResourcesRequest request,
        CancellationToken cancellationToken = default);
}
