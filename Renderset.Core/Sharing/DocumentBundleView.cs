using Renderset.Core.Tenancy;

namespace Renderset.Core.Sharing;

/// <summary>
/// Lo que necesita la página pública para pintarse. Las URLs llegan ya
/// construidas: el componente no sabe nada de rutas ni de tokens.
/// </summary>
public sealed class DocumentBundleView
{
    public required string Title { get; init; }

    public string? Message { get; init; }

    public required string Culture { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public required TenantBranding Branding { get; init; }

    public required DocumentBundleTexts Texts { get; init; }

    public required IReadOnlyList<DocumentBundleViewItem> Documents { get; init; }

    public required string DownloadAllUrl { get; init; }
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
