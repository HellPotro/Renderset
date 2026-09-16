namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class RenderedDocumentEntity
{
    public string TenantId { get; set; } = default!;

    public string DocumentId { get; set; } = default!;

    public string ReportId { get; set; } = default!;

    public string? PresetId { get; set; }

    public int PresetVersion { get; set; }

    public string Culture { get; set; } = default!;

    public string FileName { get; set; } = default!;

    public string Format { get; set; } = default!;

    /// <summary>
    /// Documento ya generado. Se guarda el contenido y no la petición: lo
    /// emitido no puede cambiar porque después se retoque el diseño.
    /// </summary>
    public string Content { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }

    public TenantEntity Tenant { get; set; } = default!;
}
