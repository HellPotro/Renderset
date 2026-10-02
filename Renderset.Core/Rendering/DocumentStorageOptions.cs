using System.Globalization;
using System.Text;

namespace Renderset.Core.Rendering;

/// <summary>
/// Sección "DocumentStorage" de la configuración de la API.
///
///     "DocumentStorage": {
///       "Provider": "AzureBlob",
///       "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
///       "ContainerName": "documents"
///     }
/// </summary>
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public DocumentStorageProvider Provider { get; set; } =
        DocumentStorageProvider.Database;

    /// <summary>
    /// Carpeta raíz para <see cref="DocumentStorageProvider.FileSystem"/>.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Cadena de conexión para <see cref="DocumentStorageProvider.AzureBlob"/>.
    /// </summary>
    public string? ConnectionString { get; set; }

    public string ContainerName { get; set; } = "documents";

    /// <summary>
    /// Ruta lógica común a todos los almacenes:
    /// {tenant}/{yyyy}/{MM}/{documentId}/{fichero}.
    ///
    /// Todos los segmentos se limpian aquí. El tenant y el nombre de fichero
    /// llegan de la petición, y un ".." en cualquiera de ellos no puede acabar
    /// escribiendo fuera de la carpeta del tenant.
    /// </summary>
    public static string BuildPath(
        string tenantId,
        string documentId,
        string fileName,
        DateTime createdAtUtc)
    {
        return string.Join(
            '/',
            CleanSegment(tenantId, "tenant"),
            createdAtUtc.ToString("yyyy", CultureInfo.InvariantCulture),
            createdAtUtc.ToString("MM", CultureInfo.InvariantCulture),
            CleanSegment(documentId, "document"),
            CleanSegment(fileName, "content"));
    }

    private static string CleanSegment(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var builder = new StringBuilder(value.Length);

        foreach (var c in value.Trim())
        {
            builder.Append(
                char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'
                    ? c
                    : '_');
        }

        var cleaned = builder.ToString();

        // ".." no puede quedar en ningún sitio, ni siquiera entre otros
        // caracteres: no es peligroso como segmento suelto ya limpio, pero
        // así no hay que pensarlo dos veces al revisar una ruta.
        while (cleaned.Contains("..", StringComparison.Ordinal))
            cleaned = cleaned.Replace("..", ".", StringComparison.Ordinal);

        cleaned = cleaned.Trim('.');

        if (cleaned.Length > 120)
            cleaned = cleaned[^120..];

        return string.IsNullOrWhiteSpace(cleaned)
            ? fallback
            : cleaned;
    }
}


public enum DocumentStorageProvider
{
    /// <summary>
    /// Columna Content de RenderedDocuments. Es el comportamiento de siempre.
    /// </summary>
    Database,

    FileSystem,

    AzureBlob
}
