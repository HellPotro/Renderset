namespace Renderset.Core.Rendering;

/// <summary>
/// Documento ya emitido. Se guarda el contenido generado, no la petición: un
/// documento entregado a un cliente no puede cambiar porque alguien retoque
/// el diseño la semana siguiente.
/// </summary>
public sealed class RenderedDocument
{
    public required string Id { get; init; }

    public required string ReportId { get; init; }

    public string? PresetId { get; init; }

    public int PresetVersion { get; init; }

    public required string Culture { get; init; }

    public required string FileName { get; init; }

    public RenderFormat Format { get; init; } = RenderFormat.Html;

    public required string Content { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
