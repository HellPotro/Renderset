namespace Renderset.Core.Rendering;

public interface IRenderedDocumentRepository
{
    Task<RenderedDocument?> GetByIdAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        RenderedDocument document,
        CancellationToken cancellationToken = default);
}
