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

    /// <summary>
    /// Añade al documento una barra con imprimir, exportar y copiar datos.
    ///
    /// Apagada por defecto a propósito. Un ERP que incrusta el HTML en su
    /// propia pantalla no quiere controles ajenos dentro; un enlace que se
    /// manda a un cliente sí. Quien pide el documento es el único que sabe
    /// en cuál de los dos casos está.
    ///
    /// La barra nunca sale impresa ni en el PDF: sólo existe en pantalla.
    /// </summary>
    public bool IncludeToolbar { get; set; }

    /// <summary>
    /// Incrusta en el documento sus datos en JSON y añade a la barra el
    /// botón de copiarlos, para quien quiera integrarlos en otro sistema.
    ///
    /// Es un interruptor aparte de la barra a propósito: una cosa es meter
    /// controles en el documento y otra meter datos. Con un solo flag se
    /// acabaría activando lo segundo sin quererlo al pedir lo primero.
    ///
    /// Sólo salen los campos y columnas visibles, con los identificadores
    /// como claves. Aun así el documento pesa casi el doble, que para una
    /// factura da igual y para un listado de miles de líneas no.
    /// </summary>
    public bool IncludeDocumentData { get; set; }
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
