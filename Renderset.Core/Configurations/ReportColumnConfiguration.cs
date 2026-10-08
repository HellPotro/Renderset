using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportColumnConfiguration
{
    public string ColumnId { get; set; } = default!;

    public bool? Visible { get; set; }

    public int? Order { get; set; }

    public decimal? Width { get; set; }

    public string? LabelKey { get; set; }

    /// <summary>
    /// Igual que en los campos: nulo = el tipo inferido.
    /// </summary>
    public ReportFieldType? Type { get; set; }

    /// <summary>
    /// Total de la columna en una fila al pie de la tabla. Nulo o None = sin
    /// total.
    /// </summary>
    public ReportColumnTotal? Total { get; set; }
}