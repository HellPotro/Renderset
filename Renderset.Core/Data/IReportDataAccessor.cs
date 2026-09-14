using System.Text.Json;

namespace Renderset.Core.Data;

public interface IReportDataAccessor
{
    object? GetValue(
        JsonElement data,
        string path);

    bool TryGetValue(
        JsonElement data,
        string path,
        out object? value);

    IReadOnlyList<JsonElement> GetCollection(
        JsonElement data,
        string path);
}