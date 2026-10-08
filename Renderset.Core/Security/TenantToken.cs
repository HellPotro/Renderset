using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Renderset.Core.Tenancy;

namespace Renderset.Core.Security;

/// <summary>
/// Token con el que RenderSet Web dice a la API para qué usuario, tenant y
/// rol hace cada llamada.
///
/// La clave de servicio sola daría acceso a todos los tenants: un fallo en
/// Web, o la clave filtrada, los abriría todos. Con el token, cada llamada
/// de Web sólo vale para el tenant del usuario que la origina, durante unos
/// minutos.
///
/// Firma ECDSA P-256: Web firma con la clave privada (Key Vault) y la API
/// sólo tiene la pública, así que quien lea la configuración de la API no
/// puede fabricar tokens.
///
///     v1.{base64url(json)}.{base64url(firma de "v1.{json}")}
///     json = { "aud": "renderset-api", "tid", "sub", "role", "iat", "exp" }
/// </summary>
public static class TenantTokenFormat
{
    public const string HeaderName = "X-Renderset-User";

    public const string Audience = "renderset-api";

    public const string Version = "v1";

    internal static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    internal static string Encode(
        ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static byte[]? Decode(
        string text)
    {
        var padded =
            text.Replace('-', '+').Replace('_', '/') +
            (text.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                0 => string.Empty,
                _ => "!"
            };

        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal sealed class Payload
    {
        [JsonPropertyName("aud")]
        public string? Audience { get; set; }

        [JsonPropertyName("tid")]
        public string? TenantId { get; set; }

        [JsonPropertyName("sub")]
        public string? UserId { get; set; }

        [JsonPropertyName("role")]
        public TenantRole Role { get; set; }

        [JsonPropertyName("iat")]
        public long IssuedAt { get; set; }

        [JsonPropertyName("exp")]
        public long ExpiresAt { get; set; }
    }
}


public sealed record TenantTokenClaims(
    string TenantId,
    string UserId,
    TenantRole Role,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);


/// <summary>
/// Firma tokens (Web). La clave privada en PEM (PKCS#8 o EC).
/// </summary>
public sealed class TenantTokenSigner
    : IDisposable
{
    private readonly ECDsa _key;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    public TenantTokenSigner(
        string privateKeyPem,
        TimeProvider? time = null,
        TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        _key = TenantTokenKeys.Import(privateKeyPem, isPrivate: true);

        if (_key.KeySize != 256)
        {
            _key.Dispose();
            throw new ArgumentException("La clave del token tiene que ser ECDSA P-256.", nameof(privateKeyPem));
        }

        _time = time ?? TimeProvider.System;
        Lifetime = lifetime ?? TimeSpan.FromMinutes(5);
    }

    public TimeSpan Lifetime { get; }

    public string Create(
        string tenantId,
        string userId,
        TenantRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = _time.GetUtcNow();

        var payload =
            JsonSerializer.SerializeToUtf8Bytes(
                new TenantTokenFormat.Payload
                {
                    Audience = TenantTokenFormat.Audience,
                    TenantId = tenantId,
                    UserId = userId,
                    Role = role,
                    IssuedAt = now.ToUnixTimeSeconds(),
                    ExpiresAt = now.Add(Lifetime).ToUnixTimeSeconds()
                },
                TenantTokenFormat.Json);

        var signed = $"{TenantTokenFormat.Version}.{TenantTokenFormat.Encode(payload)}";

        byte[] signature;

        // ECDsa no garantiza ser seguro entre hilos.
        lock (_gate)
        {
            signature = _key.SignData(
                Encoding.ASCII.GetBytes(signed),
                HashAlgorithmName.SHA256);
        }

        return $"{signed}.{TenantTokenFormat.Encode(signature)}";
    }

    public void Dispose() =>
        _key.Dispose();
}


/// <summary>
/// Valida tokens (API). Sólo necesita la clave pública en PEM.
/// </summary>
public sealed class TenantTokenValidator
    : IDisposable
{
    /// <summary>Margen para relojes de servidores que no van del todo a la par.</summary>
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Ningún token debería durar más: si llega uno así, no lo ha hecho Web.
    /// </summary>
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(30);

    private readonly ECDsa _key;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    public TenantTokenValidator(
        string publicKeyPem,
        TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);

        _key = TenantTokenKeys.Import(publicKeyPem, isPrivate: false);

        if (_key.KeySize != 256)
        {
            _key.Dispose();
            throw new ArgumentException("La clave del token tiene que ser ECDSA P-256.", nameof(publicKeyPem));
        }

        _time = time ?? TimeProvider.System;
    }

    public bool TryValidate(
        string? token,
        out TenantTokenClaims? claims,
        out string? error)
    {
        claims = null;
        error = null;

        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            return Fail("El token de usuario no tiene un formato válido.", out error);

        var parts = token.Split('.');

        if (parts.Length != 3 || parts[0] != TenantTokenFormat.Version)
            return Fail("El token de usuario no tiene un formato válido.", out error);

        var payloadBytes = TenantTokenFormat.Decode(parts[1]);
        var signature = TenantTokenFormat.Decode(parts[2]);

        if (payloadBytes is null || signature is null)
            return Fail("El token de usuario no tiene un formato válido.", out error);

        bool valid;

        lock (_gate)
        {
            valid = _key.VerifyData(
                Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                signature,
                HashAlgorithmName.SHA256);
        }

        if (!valid)
            return Fail("La firma del token de usuario no es válida.", out error);

        TenantTokenFormat.Payload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<TenantTokenFormat.Payload>(payloadBytes, TenantTokenFormat.Json);
        }
        catch (JsonException)
        {
            return Fail("El token de usuario no tiene un formato válido.", out error);
        }

        if (payload is null ||
            payload.Audience != TenantTokenFormat.Audience ||
            string.IsNullOrWhiteSpace(payload.TenantId) ||
            string.IsNullOrWhiteSpace(payload.UserId) ||
            !Enum.IsDefined(payload.Role))
        {
            return Fail("El token de usuario está incompleto.", out error);
        }

        var issued = DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAt);
        var expires = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt);
        var now = _time.GetUtcNow();

        if (expires <= issued || expires - issued > MaxLifetime)
            return Fail("El token de usuario tiene una vigencia no válida.", out error);

        if (issued > now + Skew)
            return Fail("El token de usuario es del futuro: revisa la hora del servidor.", out error);

        if (expires + Skew < now)
            return Fail("El token de usuario ha caducado.", out error);

        claims = new TenantTokenClaims(
            payload.TenantId,
            payload.UserId,
            payload.Role,
            issued,
            expires);

        return true;
    }

    private static bool Fail(
        string message,
        out string? error)
    {
        error = message;
        return false;
    }

    public void Dispose() =>
        _key.Dispose();
}


/// <summary>
/// Lectura de las claves de los tokens. Se aceptan en PEM (con sus
/// "-----BEGIN ...") o en Base64 de una sola línea, que es más cómodo en
/// user-secrets, Key Vault y la configuración de App Service:
///
///     privada: Base64 de PKCS#8           (ExportPkcs8PrivateKey)
///     pública: Base64 de SubjectPublicKeyInfo (ExportSubjectPublicKeyInfo)
/// </summary>
public static class TenantTokenKeys
{
    public static ECDsa Import(
        string text,
        bool isPrivate)
    {
        var key = ECDsa.Create();

        try
        {
            var value = text.Trim();

            if (value.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                // En variables de entorno a veces llegan los saltos de línea
                // escritos como "\n".
                key.ImportFromPem(value.Replace("\\n", "\n"));
            }
            else
            {
                var bytes = Convert.FromBase64String(value);

                if (isPrivate)
                    key.ImportPkcs8PrivateKey(bytes, out _);
                else
                    key.ImportSubjectPublicKeyInfo(bytes, out _);
            }

            return key;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            key.Dispose();

            throw new ArgumentException(
                isPrivate
                    ? "La clave privada del token de usuario no es válida (PEM o Base64 de PKCS#8, ECDSA P-256)."
                    : "La clave pública del token de usuario no es válida (PEM o Base64 de SubjectPublicKeyInfo, ECDSA P-256).",
                nameof(text),
                exception);
        }
    }
}
