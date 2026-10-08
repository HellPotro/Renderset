namespace Renderset.Core.Rendering;

/// <summary>
/// Lo que se sabe del documento que se está pintando, aparte de sus datos.
/// Lo usa el QR de la cabecera para poner el enlace del propio documento.
///
/// En el preview del diseñador todavía no hay documento: ahí
/// <see cref="IsPreview"/> deja pintar el QR con un enlace de muestra para
/// ver dónde queda. Al emitir, si falta algo, el QR no se pinta.
/// </summary>
public sealed record ReportDocumentContext
{
    public string? DocumentId { get; init; }

    /// <summary>
    /// Enlace del documento para el QR ({documentUrl}): el público firmado
    /// (sin sesión) o, si no está configurado, el del visor de Web.
    /// </summary>
    public string? DocumentUrl { get; init; }

    /// <summary>
    /// Visor interno de Web, que pide sesión ({viewerUrl}).
    /// </summary>
    public string? ViewerUrl { get; init; }

    /// <summary>
    /// Descarga directa del PDF ({pdfUrl}): el enlace público firmado
    /// acabado en /pdf, que responde con el fichero y no con una página.
    /// Sin enlace público, el mismo que <see cref="DocumentUrl"/>.
    /// </summary>
    public string? PdfUrl { get; init; }

    public bool IsPreview { get; init; }

    public static ReportDocumentContext Preview { get; } =
        new() { IsPreview = true };
}
