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
    ///
    /// Nulo cuando el contenido está en un almacén externo (blob o disco):
    /// entonces manda <see cref="ContentPath"/>.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Ruta en el almacén externo. Nula en los documentos guardados en la
    /// propia fila, que son todos los anteriores a activar el almacén.
    /// </summary>
    public string? ContentPath { get; set; }

    /// <summary>
    /// PDF generado a partir del HTML, en el almacén externo. Nulo si no se
    /// ha generado o si va en <see cref="PdfContent"/>.
    /// </summary>
    public string? PdfPath { get; set; }

    /// <summary>
    /// PDF en la propia fila, cuando no hay almacén externo.
    /// </summary>
    public byte[]? PdfContent { get; set; }

    public DateTime? PdfCreatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public TenantEntity Tenant { get; set; } = default!;
}
