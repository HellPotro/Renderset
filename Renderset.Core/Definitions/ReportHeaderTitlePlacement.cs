namespace Renderset.Core.Definitions;

/// <summary>
/// Dónde van el título y el subtítulo de la cabecera.
///
/// Con logo, datos de empresa y QR en la misma fila, el título compite por
/// el ancho con todo lo demás y la cabecera se apelotona. Sacarlo a una
/// banda propia deja arriba la fila "membrete" (logo | empresa | QR) y el
/// título a todo el ancho, como en la mayoría de documentos fiscales.
/// </summary>
public enum ReportHeaderTitlePlacement
{
    /// <summary>
    /// Junto al logo, encima de las líneas de empresa. Es el diseño de
    /// siempre y genera el mismo DOM que antes de existir la opción.
    /// </summary>
    Inline,

    /// <summary>
    /// Banda a todo el ancho debajo de la fila logo | empresa | QR.
    /// </summary>
    Below,

    /// <summary>
    /// Banda a todo el ancho encima de la fila logo | empresa | QR.
    /// </summary>
    Above
}
