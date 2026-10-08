using System.Security.Cryptography;
using System.Text;

namespace Renderset.Core.Security;

/// <summary>
/// Clave de API de un tenant: la que usa su ERP para emitir documentos.
///
/// Sólo se guarda el hash. La clave en claro se enseña una vez, al crearla;
/// si se pierde se crea otra y se revoca la vieja. El prefijo visible
/// ("rs_live_AbCd…") sirve para reconocerla en la lista sin poder usarla.
/// </summary>
public sealed class ApiKey
{
    public required Guid Id { get; init; }

    public required string TenantId { get; init; }

    /// <summary>
    /// Para qué es: "ERP producción", "Pruebas Alberto".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Primeros caracteres de la clave, para reconocerla en la lista.
    /// </summary>
    public required string Prefix { get; init; }

    public required byte[] Hash { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastUsedAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public bool IsActive(
        DateTime nowUtc) =>
        RevokedAtUtc is null &&
        (ExpiresAtUtc is null || ExpiresAtUtc > nowUtc);
}


/// <summary>
/// Formato y hash de las claves.
///
/// "rs_live_" + 256 bits aleatorios en base64url. El prefijo hace que una
/// clave filtrada se reconozca a simple vista (y que los escáneres de
/// secretos de GitHub y similares la puedan detectar).
///
/// El hash es SHA-256 sin sal: con 256 bits de entropía no hay diccionario
/// posible, y así la búsqueda es por igualdad sobre un índice.
/// </summary>
public static class ApiKeyFormat
{
    public const string Prefix = "rs_live_";

    public const int VisiblePrefixLength = 12;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Prefix +
               Convert.ToBase64String(bytes)
                   .TrimEnd('=')
                   .Replace('+', '-')
                   .Replace('/', '_');
    }

    public static bool IsWellFormed(
        string? key)
    {
        if (key is null ||
            !key.StartsWith(Prefix, StringComparison.Ordinal) ||
            key.Length != Prefix.Length + 43)
        {
            return false;
        }

        for (var i = Prefix.Length; i < key.Length; i++)
        {
            var c = key[i];

            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
                return false;
        }

        return true;
    }

    public static byte[] Hash(
        string key) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(key));

    public static string VisiblePrefix(
        string key) =>
        key.Length <= VisiblePrefixLength
            ? key
            : key[..VisiblePrefixLength];
}
