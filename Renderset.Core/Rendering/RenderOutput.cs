namespace Renderset.Core.Rendering;

public sealed class RenderOutput
{
    public RenderFormat Format { get; set; } = RenderFormat.Pdf;

    /// <summary>
    /// Admite variables: "order-{{data.numeroPedido}}.pdf".
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Qué hacer cuando el origen produce varios documentos. Es el caso
    /// normal con una consulta SQL de un rango de fechas.
    /// </summary>
    public RenderBatchMode BatchMode { get; set; } = RenderBatchMode.Single;
}

public enum RenderFormat
{
    Pdf,
    Html
}

public enum RenderBatchMode
{
    /// <summary>
    /// Se espera un único documento. Si llegan varios, es un error: casi
    /// siempre significa que la clave de agrupación está mal.
    /// </summary>
    Single,

    /// <summary>
    /// Varios documentos en un solo PDF, uno detrás de otro.
    /// </summary>
    Merged,

    /// <summary>
    /// Un fichero por documento, devueltos en un ZIP.
    /// </summary>
    Separate
}
