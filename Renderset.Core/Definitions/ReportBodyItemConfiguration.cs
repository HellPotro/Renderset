using Renderset.Core.Blocks;

namespace Renderset.Core.Definitions;

public sealed class ReportBodyItemConfiguration
{
    public required string Id { get; set; }

    public ReportBodyItemType Type { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// Coloca el elemento en la misma fila que el anterior en lugar de
    /// debajo. Nulo mantiene el comportamiento de siempre: una fila por
    /// elemento.
    ///
    /// La fila no se guarda como número a propósito. Con un índice, mover o
    /// borrar un elemento obliga a renumerar el resto y permite estados
    /// imposibles: dos elementos en la fila 3 sin fila 2. Con una bandera
    /// relativa manda el orden de la lista y no hay nada que reparar.
    /// </summary>
    public bool? SameRow { get; set; }

    /// <summary>
    /// Ancho del elemento dentro de su fila. Nulo reparte la fila a partes
    /// iguales entre sus elementos, que es lo que se espera al poner dos
    /// secciones una al lado de la otra sin tocar nada más.
    /// </summary>
    public int? Span { get; set; }

    public string? SectionId { get; set; }

    /// <summary>
    /// Referencia a un bloque reutilizable almacenado en ReportBlocks.
    /// </summary>
    public string? BlockId { get; set; }

    /// <summary>
    /// Bloque local que pertenece únicamente a este preset/report.
    /// Se utiliza para contenido rápido creado desde el diseñador.
    /// </summary>
    public ReportBodyBlockConfiguration? LocalBlock { get; set; }
}

public sealed class ReportBodyBlockConfiguration
{
    public ReportBlockType Type { get; set; }

    public string? Name { get; set; }

    public string ConfigurationJson { get; set; } = "{}";
}
