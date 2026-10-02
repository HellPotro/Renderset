namespace Renderset.Core.Rendering;

public interface IRenderedDocumentRepository
{
    Task<RenderedDocument?> GetByIdAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Metadatos de varios documentos sin cargar su contenido. Es lo que
    /// necesita un bundle para validar que existen y montar su lista; leer
    /// el HTML (o ir al blob) de cada uno sólo para eso sería tirar la red.
    /// Los que no existen simplemente no aparecen en el resultado.
    /// </summary>
    Task<IReadOnlyList<RenderedDocumentSummary>> GetSummariesAsync(
        string tenantId,
        IReadOnlyCollection<string> documentIds,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string tenantId,
        RenderedDocument document,
        CancellationToken cancellationToken = default);
}
