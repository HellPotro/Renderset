namespace Renderset.Core.Resolved;

public sealed class ResolvedReportTable
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string DataPath { get; init; }

    public List<ResolvedReportColumn> Columns { get; init; } = [];

    public ResolvedReportColumn? GetColumn(string id)
        => Columns.FirstOrDefault(
            x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase));
}