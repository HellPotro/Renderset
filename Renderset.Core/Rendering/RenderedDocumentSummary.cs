namespace Renderset.Core.Rendering;

/// <summary>
/// <see cref="RenderedDocument"/> sin el contenido.
/// </summary>
public sealed class RenderedDocumentSummary
{
    public required string Id { get; init; }

    public required string ReportId { get; init; }

    public required string Culture { get; init; }

    public required string FileName { get; init; }

    public RenderFormat Format { get; init; } = RenderFormat.Html;

    /// <summary>
    /// Si el PDF ya está generado. Aunque sea falso se puede pedir: se
    /// genera en ese momento.
    /// </summary>
    public bool HasPdf { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
