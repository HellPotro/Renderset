namespace Renderset.Core.Rendering;

/// <summary>
/// Respuesta de /api/render: lo mínimo para ir a buscar el documento, más los
/// datos con los que se emitió, que es lo que hace falta para reclamar o
/// reemitir después.
/// </summary>
public sealed class RenderDocumentResponse
{
    public required string DocumentId { get; init; }

    public required string Url { get; init; }

    public required string FileName { get; init; }

    public required string ReportId { get; init; }

    public string? PresetId { get; init; }

    public int PresetVersion { get; init; }

    public required string Culture { get; init; }

    public RenderFormat Format { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
