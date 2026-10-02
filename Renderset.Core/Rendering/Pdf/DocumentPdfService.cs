namespace Renderset.Core.Rendering.Pdf;

public interface IDocumentPdfService
{
    bool IsAvailable { get; }

    /// <summary>
    /// PDF del documento. Si ya se generó se devuelve el guardado; si no, se
    /// genera a partir del HTML emitido y se guarda. Nulo si el documento no
    /// existe.
    /// </summary>
    Task<byte[]?> GetOrCreateAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Un único PDF con los documentos en el orden dado. Los que no existen
    /// se saltan.
    /// </summary>
    Task<byte[]?> MergeAsync(
        string tenantId,
        IReadOnlyList<string> documentIds,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// El PDF se deriva siempre del HTML ya emitido y congelado, nunca de la
/// definición ni de los datos: así PDF y HTML son el mismo documento.
///
/// Se genera una vez y se guarda junto al HTML (blob o base de datos). Si
/// dos peticiones llegan a la vez antes de que exista, se genera dos veces y
/// gana la última escritura: el contenido es el mismo, así que no compensa
/// un bloqueo distribuido.
/// </summary>
public sealed class DocumentPdfService
    : IDocumentPdfService
{
    private readonly IRenderedDocumentRepository _documents;
    private readonly IPdfConverter _converter;
    private readonly PdfOptions _options;

    public DocumentPdfService(
        IRenderedDocumentRepository documents,
        IPdfConverter converter,
        PdfOptions options)
    {
        _documents = documents;
        _converter = converter;
        _options = options;
    }

    public bool IsAvailable =>
        _converter.IsAvailable;

    public async Task<byte[]?> GetOrCreateAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var stored =
            await _documents.GetPdfAsync(
                tenantId,
                documentId,
                cancellationToken);

        if (stored is not null)
            return stored;

        var document =
            await _documents.GetByIdAsync(
                tenantId,
                documentId,
                cancellationToken);

        if (document is null)
            return null;

        var pdf =
            await _converter.ConvertHtmlAsync(
                document.Content,
                _options.Page,
                cancellationToken);

        await _documents.SavePdfAsync(
            tenantId,
            documentId,
            pdf,
            cancellationToken);

        return pdf;
    }

    public async Task<byte[]?> MergeAsync(
        string tenantId,
        IReadOnlyList<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        var pdfs = new List<byte[]>(documentIds.Count);

        // En serie a propósito: el conversor ya limita la concurrencia, y
        // así un bundle de veinte documentos no acapara todos los huecos.
        foreach (var documentId in documentIds)
        {
            var pdf =
                await GetOrCreateAsync(
                    tenantId,
                    documentId,
                    cancellationToken);

            if (pdf is not null)
                pdfs.Add(pdf);
        }

        return pdfs.Count switch
        {
            0 => null,
            1 => pdfs[0],
            _ => await _converter.MergeAsync(pdfs, cancellationToken)
        };
    }
}
