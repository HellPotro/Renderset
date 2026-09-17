using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportBodyItem
{
    public required string Id { get; init; }

    public ReportBodyItemType Type { get; init; }

    public int Order { get; init; }

    /// <summary>
    /// Fila a la que pertenece el elemento, ya calculada por el resolver a
    /// partir de las banderas de la configuración. El renderer sólo agrupa
    /// por este número: no vuelve a decidir nada.
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Peso del elemento dentro de su fila, sobre un total de doce. Los
    /// elementos que van solos en su fila ocupan el ancho completo, así que
    /// aquí siempre llega un valor usable.
    /// </summary>
    public int Span { get; set; } = 12;

    public ResolvedReportSection? Section { get; init; }

    public ResolvedReportBlock? Block { get; init; }
}
