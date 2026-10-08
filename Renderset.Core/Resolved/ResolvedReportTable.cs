using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportTable
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string DataPath { get; init; }

    public List<ResolvedReportColumn> Columns { get; init; } = [];

    /// <summary>
    /// Texto de la primera celda de la fila de totales, ya traducido.
    /// </summary>
    public string TotalLabel { get; init; } = "Total";

    public bool HasTotals =>
        Columns.Any(x => x.Visible && x.Total != ReportColumnTotal.None);

    public ResolvedReportColumn? GetColumn(string id)
        => Columns.FirstOrDefault(
            x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase));
}