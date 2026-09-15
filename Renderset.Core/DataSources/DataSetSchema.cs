namespace Renderset.Core.DataSources;

public sealed class DataSetSchema
{
    public required IReadOnlyList<DataSetColumn> Columns { get; init; }

    public DataSetColumn? Find(
        string name) =>
        Columns.FirstOrDefault(x =>
            string.Equals(
                x.Name,
                name,
                StringComparison.OrdinalIgnoreCase));

    public int IndexOf(
        string name)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(
                    Columns[i].Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
