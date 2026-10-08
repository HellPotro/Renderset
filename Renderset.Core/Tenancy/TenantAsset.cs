using System.Security.Cryptography;

namespace Renderset.Core.Tenancy;

/// <summary>
/// Imagen subida por un tenant para sus documentos (de momento, el logo).
///
/// El id es el SHA-256 del contenido (32 primeros caracteres): la misma
/// imagen subida dos veces es la misma fila, y la URL cambia cuando cambia
/// la imagen, así que se puede cachear para siempre sin que nadie vea un
/// logo viejo.
///
/// Se sirve en público (GET /assets/{tenant}/{id}.{ext}) porque la pintan
/// páginas sin login (el visor de un documento compartido) y el conversor
/// de PDF. No es un secreto: es la imagen que va impresa en el documento.
/// </summary>
public sealed class TenantAsset
{
    public required string TenantId { get; init; }

    public required string AssetId { get; init; }

    public required string ContentType { get; init; }

    public string? FileName { get; init; }

    public long SizeBytes { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string? CreatedBy { get; init; }

    public string Extension =>
        TenantAssetRules.ExtensionFor(ContentType);

    public string PublicFileName =>
        $"{AssetId}.{Extension}";
}

public interface ITenantAssetRepository
{
    /// <summary>
    /// Guarda la imagen si no estaba ya (mismo tenant y contenido).
    /// </summary>
    Task<TenantAsset> SaveAsync(
        TenantAsset asset,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<(TenantAsset Asset, byte[] Content)?> GetAsync(
        string tenantId,
        string assetId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Respuesta de POST /api/assets/{tenantId}/logo.
/// </summary>
public sealed class TenantAssetResponse
{
    public required string Url { get; init; }

    public required string AssetId { get; init; }

    public required string ContentType { get; init; }

    public long SizeBytes { get; init; }
}

/// <summary>
/// Qué imágenes se aceptan. Se mira el contenido (los primeros bytes) y no
/// la extensión ni el Content-Type que manda el navegador.
///
/// SVG no: es XML con scripts posibles, y se serviría desde el dominio de
/// la API a cualquiera.
/// </summary>
public static class TenantAssetRules
{
    /// <summary>
    /// Un logo no necesita más, y el DeCA tiene un límite de 5 MB por PDF
    /// en el que el logo va dentro.
    /// </summary>
    public const long MaxLogoBytes = 1024 * 1024;

    public const string Accept =
        ".png,.jpg,.jpeg,.webp,.gif,image/png,image/jpeg,image/webp,image/gif";

    /// <summary>
    /// Tipo de imagen por su firma, o nulo si no es una de las admitidas.
    /// </summary>
    public static string? DetectContentType(
        ReadOnlySpan<byte> content)
    {
        if (content.Length >= 8 &&
            content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47 &&
            content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
        {
            return "image/png";
        }

        if (content.Length >= 3 &&
            content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (content.Length >= 12 &&
            content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F' &&
            content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            return "image/webp";
        }

        if (content.Length >= 6 &&
            content[0] == (byte)'G' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'8' &&
            (content[4] == (byte)'7' || content[4] == (byte)'9') && content[5] == (byte)'a')
        {
            return "image/gif";
        }

        return null;
    }

    public static string ExtensionFor(
        string contentType) =>
        contentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            "image/gif" => "gif",
            _ => "bin"
        };

    public static string IdFor(
        ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content))[..32].ToLowerInvariant();

    /// <summary>
    /// "{id}.{ext}" de la URL pública → id, si tiene la forma esperada.
    /// </summary>
    public static string? ParsePublicFileName(
        string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var dot = fileName.IndexOf('.');
        var id = dot < 0 ? fileName : fileName[..dot];

        return id.Length == 32 && id.All(Uri.IsHexDigit)
            ? id.ToLowerInvariant()
            : null;
    }
}
