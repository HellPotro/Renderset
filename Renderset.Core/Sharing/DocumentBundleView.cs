using Renderset.Core.Tenancy;

namespace Renderset.Core.Sharing;

/// <summary>
/// Lo que necesita la página pública para pintarse. Las URLs llegan ya
/// construidas: el componente no sabe nada de rutas ni de tokens.
/// </summary>
public sealed class DocumentBundleView
{
    public required string Title { get; init; }

    /// <summary>
    /// El del bundle o, si no tiene, el mensaje por defecto del tenant.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Pie configurado por el tenant (contacto). Opcional.
    /// </summary>
    public string? FooterText { get; init; }

    public required string Culture { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>
    /// Falso en el enlace público de un único documento (el del QR), que no
    /// caduca: no se enseña "disponible hasta".
    /// </summary>
    public bool ShowExpiry { get; init; } = true;

    public required TenantBranding Branding { get; init; }

    public required DocumentBundleTexts Texts { get; init; }

    public required IReadOnlyList<DocumentBundleViewItem> Documents { get; init; }

    /// <summary>
    /// ZIP con todos los documentos.
    /// </summary>
    public required string DownloadAllUrl { get; init; }

    /// <summary>
    /// Todos los documentos en un único PDF. Nulo sin conversor de PDF.
    /// </summary>
    public string? DownloadAllPdfUrl { get; init; }

    /// <summary>
    /// Si las descargas de <see cref="DocumentBundleViewItem.DownloadUrl"/>
    /// son PDF (cambia el texto del botón).
    /// </summary>
    public bool PdfAvailable { get; init; }

    /// <summary>
    /// Enseña Exportar CSV y Descargar JSON. Aun así, cada botón sólo
    /// aparece si el documento abierto trae datos.
    /// </summary>
    public bool AllowDataDownload { get; init; }
}


public sealed class DocumentBundleViewItem
{
    public required int Position { get; init; }

    public required string Title { get; init; }

    public required string ViewUrl { get; init; }

    public required string DownloadUrl { get; init; }
}


public sealed class DocumentBundleUnavailableView
{
    public required DocumentBundleOpenStatus Reason { get; init; }

    public string? FooterText { get; init; }

    public required string Culture { get; init; }

    public required TenantBranding Branding { get; init; }

    public required DocumentBundleTexts Texts { get; init; }
}


/// <summary>
/// Convierte las vistas en HTML. Interfaz en Core e implementación en
/// Renderset.Blazor, igual que <see cref="Rendering.IReportDocumentRenderer"/>.
/// </summary>
public interface IDocumentBundlePageRenderer
{
    Task<string> RenderViewerAsync(
        DocumentBundleView view,
        CancellationToken cancellationToken = default);

    Task<string> RenderUnavailableAsync(
        DocumentBundleUnavailableView view,
        CancellationToken cancellationToken = default);
}
