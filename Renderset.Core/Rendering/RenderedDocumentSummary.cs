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

    public DateTime CreatedAtUtc { get; init; }
}
