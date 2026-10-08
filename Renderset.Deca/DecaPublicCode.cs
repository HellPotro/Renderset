using System.Security.Cryptography;

namespace Renderset.Deca;

/// <summary>
/// Código del enlace público del QR: 16 bytes aleatorios en base64url (22
/// caracteres, 128 bits). No se puede adivinar ni deducir de otro, y deja
/// una URL lo bastante corta para que el QR impreso se lea bien.
///
/// Se guarda tal cual (no un hash, como en los bundles): el documento al
/// que da acceso está en la misma base de datos, así que esconder el código
/// no protegería nada y obligaría a no poder volver a enseñarlo.
/// </summary>
public static class DecaPublicCode
{
    public const int Length = 22;

    public static string New()
    {
        Span<byte> bytes = stackalloc byte[16];

        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Forma válida, para descartar sin ir a la base de datos lo que no
    /// puede ser un código.
    /// </summary>
    public static bool IsWellFormed(
        string? code) =>
        code is { Length: Length } &&
        code.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
}
