namespace Renderset.Core.Security;

public interface IApiKeyService
{
    /// <summary>
    /// Crea la clave y la devuelve en claro. Es la única vez que existe así.
    /// </summary>
    Task<CreatedApiKey> CreateAsync(
        string tenantId,
        string name,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string tenantId,
        Guid keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clave activa para ese valor, o nulo. No distingue entre "no existe",
    /// "revocada" y "caducada": quien prueba claves no tiene por qué saberlo.
    /// </summary>
    Task<ApiKey?> AuthenticateAsync(
        string? rawKey,
        CancellationToken cancellationToken = default);
}


public sealed class CreatedApiKey
{
    public required ApiKey Key { get; init; }

    public required string PlainText { get; init; }
}


public sealed class ApiKeyService
    : IApiKeyService
{
    public const int MaxNameLength = 100;

    /// <summary>
    /// La fecha de último uso se actualiza como mucho cada tanto: escribir
    /// en cada petición convertiría cada lectura de la API en una escritura.
    /// </summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    private readonly IApiKeyRepository _keys;
    private readonly TimeProvider _time;

    public ApiKeyService(
        IApiKeyRepository keys,
        TimeProvider time)
    {
        _keys = keys;
        _time = time;
    }

    public async Task<CreatedApiKey> CreateAsync(
        string tenantId,
        string name,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = UtcNow();

        if (expiresAtUtc is { } expires && expires <= now)
            throw new ArgumentException("La caducidad tiene que ser futura.", nameof(expiresAtUtc));

        var plain = ApiKeyFormat.Generate();

        var trimmed = name.Trim();

        var key =
            new ApiKey
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = trimmed.Length > MaxNameLength
                    ? trimmed[..MaxNameLength]
                    : trimmed,
                Prefix = ApiKeyFormat.VisiblePrefix(plain),
                Hash = ApiKeyFormat.Hash(plain),
                CreatedAtUtc = now,
                ExpiresAtUtc = expiresAtUtc
            };

        await _keys.CreateAsync(key, cancellationToken);

        return new CreatedApiKey
        {
            Key = key,
            PlainText = plain
        };
    }

    public Task<IReadOnlyList<ApiKey>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        _keys.ListAsync(tenantId, cancellationToken);

    public Task<bool> RevokeAsync(
        string tenantId,
        Guid keyId,
        CancellationToken cancellationToken = default) =>
        _keys.RevokeAsync(tenantId, keyId, UtcNow(), cancellationToken);

    public async Task<ApiKey?> AuthenticateAsync(
        string? rawKey,
        CancellationToken cancellationToken = default)
    {
        // Lo que no tiene forma de clave no llega a la base de datos.
        if (!ApiKeyFormat.IsWellFormed(rawKey))
            return null;

        var key =
            await _keys.FindByHashAsync(
                ApiKeyFormat.Hash(rawKey!),
                cancellationToken);

        var now = UtcNow();

        if (key is null || !key.IsActive(now))
            return null;

        if (key.LastUsedAtUtc is null ||
            now - key.LastUsedAtUtc.Value > TouchInterval)
        {
            await _keys.TouchAsync(key.Id, now, cancellationToken);
        }

        return key;
    }

    private DateTime UtcNow() =>
        _time.GetUtcNow().UtcDateTime;
}
