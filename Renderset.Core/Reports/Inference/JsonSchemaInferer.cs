using System.Globalization;
using System.Text.Json;

namespace Renderset.Core.Reports.Inference;

public static class JsonSchemaInferer
{
    public static ReportDataSchema Infer(
        JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "El JSON raíz debe ser un objeto.",
                nameof(root));
        }

        var schema =
            new ReportDataSchema();

        foreach (var property in root.EnumerateObject())
        {
            schema.Fields.Add(
                InferField(
                    property.Name,
                    property.Name,
                    property.Value));
        }

        return schema;
    }

    private static ReportDataField InferField(
        string name,
        string path,
        JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object =>
                InferObject(
                    name,
                    path,
                    value),

            JsonValueKind.Array =>
                InferArray(
                    name,
                    path,
                    value),

            JsonValueKind.String =>
                InferString(
                    name,
                    path,
                    value),

            JsonValueKind.Number =>
                CreateField(
                    name,
                    path,
                    ReportDataType.Number),

            JsonValueKind.True or
            JsonValueKind.False =>
                CreateField(
                    name,
                    path,
                    ReportDataType.Boolean),

            JsonValueKind.Null or
            JsonValueKind.Undefined =>
                CreateField(
                    name,
                    path,
                    ReportDataType.Null,
                    nullable: true),

            _ =>
                CreateField(
                    name,
                    path,
                    ReportDataType.String)
        };
    }

    private static ReportDataField InferObject(
        string name,
        string path,
        JsonElement value)
    {
        var field =
            CreateField(
                name,
                path,
                ReportDataType.Object);

        foreach (var property in value.EnumerateObject())
        {
            var childPath =
                $"{path}.{property.Name}";

            field.Children.Add(
                InferField(
                    property.Name,
                    childPath,
                    property.Value));
        }

        return field;
    }

    private static ReportDataField InferArray(
        string name,
        string path,
        JsonElement value)
    {
        var field =
            CreateField(
                name,
                path,
                ReportDataType.Array);

        var items =
            value.EnumerateArray()
                .ToList();

        if (items.Count == 0)
        {
            return field;
        }

        var firstNonNull =
            items.FirstOrDefault(
                x =>
                    x.ValueKind != JsonValueKind.Null &&
                    x.ValueKind != JsonValueKind.Undefined);

        if (firstNonNull.ValueKind == JsonValueKind.Undefined)
        {
            return field;
        }

        if (firstNonNull.ValueKind == JsonValueKind.Object)
        {
            field.Children =
                InferArrayObjectChildren(
                    path,
                    items);

            return field;
        }

        field.Children.Add(
            InferField(
                "item",
                path,
                firstNonNull));

        return field;
    }

    private static List<ReportDataField> InferArrayObjectChildren(
        string arrayPath,
        IReadOnlyCollection<JsonElement> items)
    {
        var result =
            new Dictionary<
                string,
                ReportDataField>(
                StringComparer.OrdinalIgnoreCase);

        var objectItems =
            items
                .Where(
                    x =>
                        x.ValueKind ==
                        JsonValueKind.Object)
                .ToList();

        foreach (var item in objectItems)
        {
            foreach (var property in item.EnumerateObject())
            {
                var childPath =
                    $"{arrayPath}.{property.Name}";

                if (!result.TryGetValue(
                        property.Name,
                        out var existing))
                {
                    result[property.Name] =
                        InferField(
                            property.Name,
                            childPath,
                            property.Value);

                    continue;
                }

                MergeField(
                    existing,
                    property.Value);
            }
        }

        /*
         * Si un campo no está presente en todos
         * los elementos del array, lo consideramos nullable.
         */
        foreach (var field in result.Values)
        {
            var occurrences =
                objectItems.Count(
                    item =>
                        item.TryGetProperty(
                            field.Name,
                            out _));

            if (occurrences < objectItems.Count)
            {
                field.Nullable = true;
            }
        }

        return result.Values.ToList();
    }

    private static void MergeField(
        ReportDataField field,
        JsonElement value)
    {
        if (value.ValueKind is
            JsonValueKind.Null or
            JsonValueKind.Undefined)
        {
            field.Nullable = true;
            return;
        }

        var inferred =
            InferField(
                field.Name,
                field.Path,
                value);

        if (field.Type == ReportDataType.Null)
        {
            field.Type =
                inferred.Type;

            field.Children =
                inferred.Children;

            field.Nullable = true;

            return;
        }

        if (field.Type != inferred.Type)
        {
            field.Type =
                ResolveCompatibleType(
                    field.Type,
                    inferred.Type);
        }

        if (field.Type == ReportDataType.Object)
        {
            MergeChildren(
                field,
                inferred);
        }
    }

    private static void MergeChildren(
        ReportDataField target,
        ReportDataField source)
    {
        foreach (var sourceChild in source.Children)
        {
            var targetChild =
                target.Children.FirstOrDefault(
                    x =>
                        string.Equals(
                            x.Name,
                            sourceChild.Name,
                            StringComparison.OrdinalIgnoreCase));

            if (targetChild is null)
            {
                sourceChild.Nullable = true;

                target.Children.Add(
                    sourceChild);

                continue;
            }

            MergeFields(
                targetChild,
                sourceChild);
        }
    }

    private static void MergeFields(
        ReportDataField target,
        ReportDataField source)
    {
        target.Nullable |=
            source.Nullable;

        if (target.Type != source.Type)
        {
            target.Type =
                ResolveCompatibleType(
                    target.Type,
                    source.Type);
        }

        if (target.Type ==
            ReportDataType.Object)
        {
            MergeChildren(
                target,
                source);
        }
    }

    private static ReportDataType ResolveCompatibleType(
        ReportDataType current,
        ReportDataType incoming)
    {
        if (current == incoming)
        {
            return current;
        }

        if (current == ReportDataType.Null)
        {
            return incoming;
        }

        if (incoming == ReportDataType.Null)
        {
            return current;
        }

        /*
         * Fecha + DateTime:
         * nos quedamos con DateTime.
         */
        if (
            (current == ReportDataType.Date &&
             incoming == ReportDataType.DateTime)
            ||
            (current == ReportDataType.DateTime &&
             incoming == ReportDataType.Date)
        )
        {
            return ReportDataType.DateTime;
        }

        /*
         * Si hay tipos incompatibles,
         * usamos String como fallback seguro.
         *
         * Ejemplo:
         * quantity = 10
         * quantity = "N/A"
         */
        return ReportDataType.String;
    }

    private static ReportDataField InferString(
        string name,
        string path,
        JsonElement value)
    {
        var text =
            value.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return CreateField(
                name,
                path,
                ReportDataType.String);
        }

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return CreateField(
                name,
                path,
                ReportDataType.Date);
        }

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            return CreateField(
                name,
                path,
                ReportDataType.DateTime);
        }

        return CreateField(
            name,
            path,
            ReportDataType.String);
    }

    private static ReportDataField CreateField(
        string name,
        string path,
        ReportDataType type,
        bool nullable = false)
    {
        return new ReportDataField
        {
            Name = name,
            Path = path,
            Type = type,
            Nullable = nullable
        };
    }
}