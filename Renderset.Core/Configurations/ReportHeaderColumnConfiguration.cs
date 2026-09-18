using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

/// <summary>
/// Columna de contenido dentro de una cabecera. Las líneas conservan su
/// identidad y su TextKey para que reordenar columnas no rompa traducciones.
/// </summary>
public sealed class ReportHeaderColumnConfiguration
{
    public string Id { get; set; } = default!;

    public ReportHorizontalAlignment Align { get; set; } =
        ReportHorizontalAlignment.Left;

    /// <summary>
    /// Anchura relativa expresada como porcentaje. El renderer la trata como
    /// flex-basis, por lo que varias columnas pueden repartirse el espacio sin
    /// introducir una rejilla rígida en el modelo.
    /// </summary>
    public int Width { get; set; } = 50;

    public List<ReportHeaderLineConfiguration> Lines { get; set; } = [];
}
