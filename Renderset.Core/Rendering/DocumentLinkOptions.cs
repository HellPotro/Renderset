using System.Security.Cryptography;
using System.Text;

namespace Renderset.Core.Rendering;

/// <summary>
/// Sección "DocumentLinks" de ApiService: a dónde lleva el QR de la cabecera
/// ({documentUrl}).
///
///     "DocumentLinks": {
///       "BaseUrl": "https://app.renderset.app",          visor interno de Web (con sesión)
///       "PublicBaseUrl": "https://docs.renderset.app",   visor público (sin sesión)
///       "Public": true,
///       "SigningKey": ""                                 opcional, ver abajo
///     }
///
/// Con <see cref="Public"/> (por defecto) el QR lleva un enlace público que
/// abre cualquiera que lo lea, sin cuenta: /d/{tenant}/{documento}/{firma}.
/// La firma es un HMAC del tenant y el documento, así que el enlace no se
/// puede fabricar a partir de otro ni sabiendo el id de un documento: sólo
/// existe el que va impreso.
///
/// La clave de la firma es <see cref="SigningKey"/> o, si no se configura,
/// una derivada de la clave de servicio de la API. Cambiarla invalida los QR
/// ya impresos: si se rota la clave de servicio, conviene fijar antes
/// SigningKey con una propia.
///
/// Sin dominio público ni clave, el QR vuelve a ser el enlace del visor de
/// Web, que pide sesión (comportamiento anterior).
/// </summary>
public sealed class DocumentLinkOptions
{
    public const string SectionName = "DocumentLinks";

    public const string PublicPrefix = "/d";

    /// <summary>
    /// Bytes de la firma que van en la URL: 16 (128 bits, 22 caracteres).
    /// </summary>
    private const int SignatureBytes = 16;

    /// <summary>
    /// URL de RenderSet Web: visor interno /documents/{id}.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Dominio público de la API para el visor sin sesión. Vacío = el de
    /// DocumentSharing:PublicBaseUrl (lo rellena la API al arrancar).
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Falso = el QR lleva siempre el visor interno (pide sesión).
    /// </summary>
    public bool Public { get; set; } = true;

    /// <summary>
    /// Clave propia de la firma (32 caracteres o más). Vacía = derivada de
    /// la clave de servicio.
    /// </summary>
    public string? SigningKey { get; set; }

    private byte[]? _key;

    /// <summary>
    /// Fija la clave de firma. La llama la API al arrancar con
    /// <see cref="SigningKey"/> o, en su defecto, con la clave de servicio.
    /// </summary>
    public DocumentLinkOptions UseSigningSecret(
        string? secret)
    {
        _key =
            string.IsNullOrWhiteSpace(secret) || secret.Trim().Length < 32
                ? null
                : HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    Encoding.UTF8.GetBytes(secret.Trim()),
                    32,
                    info: Encoding.UTF8.GetBytes("renderset-document-links-v1"));

        return this;
    }

    public bool CanSign => _key is not null;

    /// <summary>
    /// Enlace del visor interno de Web, o nulo sin BaseUrl válida (sólo
    /// http/https).
    /// </summary>
    public string? DocumentUrl(
        string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId) ||
            Absolute(BaseUrl) is not { } baseUri)
        {
            return null;
        }

        return $"{baseUri}/documents/{Uri.EscapeDataString(documentId)}";
    }

    /// <summary>
    /// Enlace que lleva el QR: el público si se puede, si no el interno.
    /// </summary>
    public string? DocumentUrl(
        string tenantId,
        string documentId) =>
        PublicDocumentUrl(tenantId, documentId)
        ?? DocumentUrl(documentId);

    /// <summary>
    /// Enlace público firmado, o nulo si no está activado o falta dominio o
    /// clave.
    /// </summary>
    public string? PublicDocumentUrl(
        string tenantId,
        string documentId)
    {
        if (!Public ||
            string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(documentId) ||
            Absolute(PublicBaseUrl) is not { } baseUri ||
            Sign(tenantId, documentId) is not { } signature)
        {
            return null;
        }

        return baseUri + PublicPath(tenantId, documentId, signature);
    }

    /// <summary>
    /// Descarga directa del PDF: lo que lleva el QR con {pdfUrl}. Sin
    /// enlace público, el visor interno (no hay PDF sin sesión fuera de
    /// /d/...).
    /// </summary>
    public string? PdfUrl(
        string tenantId,
        string documentId) =>
        PublicDocumentUrl(tenantId, documentId) is { } url
            ? url + "/pdf"
            : DocumentUrl(documentId);

    public static string PublicPath(
        string tenantId,
        string documentId,
        string signature) =>
        $"{PublicPrefix}/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(documentId)}/{signature}";

    public string? Sign(
        string tenantId,
        string documentId)
    {
        if (_key is null)
            return null;

        var hash =
            HMACSHA256.HashData(
                _key,
                Encoding.UTF8.GetBytes($"{tenantId}\n{documentId}"));

        return Base64Url(hash.AsSpan(0, SignatureBytes));
    }

    /// <summary>
    /// Comprueba la firma en tiempo constante.
    /// </summary>
    public bool Verify(
        string tenantId,
        string documentId,
        string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            Sign(tenantId, documentId) is not { } expected)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.Trim()));
    }

    private static string? Absolute(
        string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string Base64Url(
        ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
