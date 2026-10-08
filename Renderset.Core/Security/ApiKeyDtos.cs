namespace Renderset.Core.Security;

/// <summary>
/// Clave tal como se enseña en la gestión: sin hash ni clave en claro.
/// </summary>
public sealed class ApiKeyResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Prefix { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastUsedAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public bool Active { get; init; }

    public static ApiKeyResponse From(
        ApiKey key,
        DateTime nowUtc) =>
        new()
        {
            Id = key.Id,
            Name = key.Name,
            Prefix = key.Prefix,
            CreatedAtUtc = key.CreatedAtUtc,
            LastUsedAtUtc = key.LastUsedAtUtc,
            ExpiresAtUtc = key.ExpiresAtUtc,
            RevokedAtUtc = key.RevokedAtUtc,
            Active = key.IsActive(nowUtc)
        };
}


public sealed class CreateApiKeyRequest
{
    public string? Name { get; set; }

    /// <summary>
    /// Nulo: no caduca. Para claves de pruebas conviene ponerla.
    /// </summary>
    public int? ExpiresInDays { get; set; }
}


/// <summary>
/// Respuesta de la creación: la única vez que viaja la clave en claro.
/// </summary>
public sealed class CreatedApiKeyResponse
{
    public required ApiKeyResponse Key { get; init; }

    public required string PlainText { get; init; }
}
