namespace Renderset.Core.Localization;

public interface ITenantCultureRepository
{
    Task<IReadOnlyCollection<TenantCulture>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantCulture?> GetDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        TenantCulture culture,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string tenantId,
        string culture,
        CancellationToken cancellationToken = default);
}
