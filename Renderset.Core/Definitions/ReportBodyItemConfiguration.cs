using Renderset.Core.Blocks;

namespace Renderset.Core.Definitions;

public sealed class ReportBodyItemConfiguration
{
    public required string Id { get; set; }

    public ReportBodyItemType Type { get; set; }

    public int Order { get; set; }

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
