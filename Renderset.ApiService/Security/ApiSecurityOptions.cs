using System.Security.Cryptography;
using Renderset.Core.Security;

namespace Renderset.Api.Security;

/// <summary>
/// Sección "Security" de la configuración de la API.
///
///     "Security": {
///       "ServiceKeys": [ { "Name": "web", "Key": "(user-secrets / Key Vault)" } ]
///     }
///
/// Las claves de servicio son para aplicaciones de confianza (RenderSet
/// Web), que pueden actuar sobre cualquier tenant porque controlan ellas
/// mismas a qué tenant pertenece cada usuario. Nunca se le dan a un cliente:
/// para eso están las API keys de tenant.
/// </summary>
public sealed class ApiSecurityOptions
{
    public const string SectionName = "Security";

    public const int MinServiceKeyLength = 32;

    public List<ServiceKeyOptions> ServiceKeys { get; set; } = [];

    /// <summary>
    /// Clave pública (PEM, ECDSA P-256) con la que se validan los tokens de
    /// usuario que manda Web (<see cref="Renderset.Core.Security.TenantTokenFormat"/>).
    /// La privada sólo la tiene Web.
    /// </summary>
    public string? UserTokenPublicKey { get; set; }

    /// <summary>
    /// Si una clave de servicio tiene que venir siempre con token de usuario.
    /// Nulo = sí, salvo en desarrollo. En producción tiene que estar
    /// encendido: sin él, la clave de servicio sola abre todos los tenants.
    /// </summary>
    public bool? RequireUserToken { get; set; }
}


public sealed class ServiceKeyOptions
{
    public string Name { get; set; } = "service";

    public string Key { get; set; } = string.Empty;
}


/// <summary>
/// Hashes de las claves de servicio, calculados una vez al arrancar para
/// comparar en tiempo constante sin guardar la clave en claro en memoria más
/// de lo necesario.
/// </summary>
public sealed class ServiceKeyRegistry
{
    private readonly IReadOnlyList<(string Name, byte[] Hash)> _keys;

    public ServiceKeyRegistry(
        ApiSecurityOptions options)
    {
        var keys = new List<(string, byte[])>();

        foreach (var key in options.ServiceKeys)
        {
            if (string.IsNullOrWhiteSpace(key.Key))
                continue;

            // Una clave corta en una aplicación con permisos sobre todos los
            // tenants es demasiado fácil de adivinar: mejor no arrancar.
            if (key.Key.Length < ApiSecurityOptions.MinServiceKeyLength)
            {
                throw new InvalidOperationException(
                    $"La clave de servicio '{key.Name}' tiene que tener al menos " +
                    $"{ApiSecurityOptions.MinServiceKeyLength} caracteres.");
            }

            keys.Add((key.Name, ApiKeyFormat.Hash(key.Key)));
        }

        _keys = keys;
    }

    public bool HasKeys =>
        _keys.Count > 0;

    public string? Match(
        byte[] hash)
    {
        string? match = null;

        // Se recorren todas aunque ya haya coincidencia: que el tiempo no
        // dependa de cuál ha coincidido.
        foreach (var (name, expected) in _keys)
        {
            if (CryptographicOperations.FixedTimeEquals(hash, expected))
                match ??= name;
        }

        return match;
    }
}
