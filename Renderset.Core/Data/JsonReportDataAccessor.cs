using System.Globalization;
using System.Text.Json;

namespace Renderset.Core.Data;

public sealed class JsonReportDataAccessor
    : IReportDataAccessor
{
    public object? GetValue(
        JsonElement data,
        string path)
    {
        return TryGetValue(
            data,
            path,
            out var value)
                ? value
                : null;
    }

    public bool TryGetValue(
        JsonElement data,
        string path,
        out object? value)
    {
        value = null;

        if (!TryGetElement(
                data,
                path,
                out var element))
        {
            return false;
        }

        value = ConvertValue(element);

        return true;
    }

    public IReadOnlyList<JsonElement> GetCollection(
        JsonElement data,
        string path)
    {
        if (!TryGetElement(
                data,
                path,
                out var element))
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Select(x => x.Clone())
            .ToList();
    }

    private static bool TryGetElement(
        JsonElement data,
        string path,
        out JsonElement result)
    {
        result = data;

        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var parts = path.Split(
            '.',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                result = default;
                return false;
            }

            if (!TryGetPropertyIgnoreCase(
                    result,
                    part,
                    out result))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.TryGetProperty(
                name,
                out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;

        return false;
    }

    private static object? ConvertValue(
        JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String =>
                ConvertString(element),

            JsonValueKind.Number
                when element.TryGetDecimal(out var decimalValue) =>
                decimalValue,

            JsonValueKind.Number
                when element.TryGetInt64(out var integerValue) =>
                integerValue,

            JsonValueKind.True =>
                true,

            JsonValueKind.False =>
                false,

            JsonValueKind.Null or
            JsonValueKind.Undefined =>
                null,

            JsonValueKind.Object or
            JsonValueKind.Array =>
                element.Clone(),

            _ =>
                element.ToString()
        };
    }

    private static object? ConvertString(
        JsonElement element)
    {
        var value = element.GetString();

        if (value is null)
            return null;

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var date))
        {
            return date;
        }

        return value;
    }
}