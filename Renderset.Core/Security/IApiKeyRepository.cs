namespace Renderset.Core.Security;

public interface IApiKeyRepository
{
    Task<ApiKey?> FindByHashAsync(
        byte[] hash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        ApiKey key,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string tenantId,
        Guid keyId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    Task TouchAsync(
        Guid keyId,
        DateTime usedAtUtc,
        CancellationToken cancellationToken = default);
}
