using System.Security.Cryptography;
using System.Text;

namespace Renderset.Core.Sharing;

/// <summary>
/// Token del enlace público de un bundle.
///
/// 256 bits aleatorios en base64url: 43 caracteres que viajan en la URL sin
/// escapar nada. El Guid del bundle no sirve como secreto: identifica, pero
/// no está pensado para ser imposible de adivinar ni se puede rotar sin
/// cambiar de bundle.
/// </summary>
public static class BundleToken
{
    public const int ByteLength = 32;

    public const int TextLength = 43;

    public const int HashLength = 32;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Hash(
        string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        return SHA256.HashData(
            Encoding.ASCII.GetBytes(token));
    }

    /// <summary>
    /// Descarta lo que no puede ser un token antes de tocar la base de datos.
    /// Una URL truncada al copiarla de un correo es el caso típico.
    /// </summary>
    public static bool IsWellFormed(
        string? token)
    {
        if (token is null || token.Length != TextLength)
            return false;

        foreach (var c in token)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Comparación en tiempo constante: con una comparación normal, lo que
    /// tarda en fallar dice cuántos bytes del hash se han acertado.
    /// </summary>
    public static bool Matches(
        string? token,
        byte[]? expectedHash)
    {
        if (!IsWellFormed(token) ||
            expectedHash is null ||
            expectedHash.Length != HashLength)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Hash(token!),
            expectedHash);
    }
}
