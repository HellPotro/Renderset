using Renderset.Core.Definitions;

namespace Renderset.Core.Reports.Inference;

public static class ReportDefinitionFactory
{
    public static ReportDefinition Create(
        string reportId,
        string reportName,
        ReportDataSchema schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportName);
        ArgumentNullException.ThrowIfNull(schema);

        var sections = new List<ReportSectionDefinition>();

        var rootFields =
            schema.Fields
                .Where(IsSimpleField)
                .ToList();

        var order = 10;

        if (rootFields.Count > 0)
        {
            sections.Add(
                CreateFieldsSection(
                    id: "general",
                    name: "General",
                    fields: rootFields,
                    order: order));

            order += 10;
        }

        foreach (var field in schema.Fields)
        {
            if (field.Type == ReportDataType.Object)
            {
                sections.Add(
                    CreateFieldsSection(
                        id: field.Path,
                        name: ToLabel(field.Name),
                        fields: field.Children,
                        order: order));

                order += 10;

                continue;
            }

            if (field.Type == ReportDataType.Array)
            {
                sections.Add(
                    CreateTableSection(
                        field,
                        order));

                order += 10;
            }
        }

        return new ReportDefinition
        {
            Id = reportId,
            Name = reportName,
            Version = 1,
            Header = new ReportHeaderDefinition
            {
                Title = reportName,
                Subtitle = null,
                AllowLogo = true,
                Fields = []
            },
            Sections = sections,
            Footer = new ReportFooterDefinition
            {
                VisibleByDefault = true,
                Text = null,
                ShowGenerationDate = false,
                AllowPageNumber = true
            }
        };
    }

    private static ReportSectionDefinition CreateFieldsSection(
        string id,
        string name,
        IReadOnlyCollection<ReportDataField> fields,
        int order)
    {
        return new ReportSectionDefinition
        {
            Id = id,
            Name = name,
            Order = order,
            VisibleByDefault = true,

            Fields =
                fields
                    .Where(IsSimpleField)
                    .Select(
                        (field, index) =>
                            CreateFieldDefinition(
                                field,
                                (index + 1) * 10))
                    .ToList()
        };
    }

    private static ReportSectionDefinition CreateTableSection(
        ReportDataField arrayField,
        int order)
    {
        return new ReportSectionDefinition
        {
            Id = arrayField.Path,
            Name = ToLabel(arrayField.Name),
            Order = order,
            VisibleByDefault = true,

            Fields = [],

            Table = new ReportTableDefinition
            {
                Id = $"{arrayField.Path}.table",
                Name = ToLabel(arrayField.Name),
                DataPath = arrayField.Path,

                Columns =
                    arrayField.Children
                        .Where(IsSimpleField)
                        .Select(
                            (field, index) =>
                                CreateColumnDefinition(
                                    field,
                                    (index + 1) * 10))
                        .ToList()
            }
        };
    }

    private static ReportFieldDefinition CreateFieldDefinition(
        ReportDataField field,
        int order)
    {
        return new ReportFieldDefinition
        {
            Id = field.Path,
            Label = ToLabel(field.Name),
            DataPath = field.Path,
            Order = order,
            VisibleByDefault = true,
            Type = MapFieldType(field.Type)
        };
    }

    private static ReportColumnDefinition CreateColumnDefinition(
        ReportDataField field,
        int order)
    {
        /*
         * Dentro de una tabla el DataPath es relativo
         * al elemento del array.
         *
         * lines.reference -> reference
         */
        var relativeDataPath =
            field.Path.Contains('.')
                ? field.Path[(field.Path.LastIndexOf('.') + 1)..]
                : field.Path;

        return new ReportColumnDefinition
        {
            Id = field.Path,
            Label = ToLabel(field.Name),
            DataPath = relativeDataPath,
            Order = order,
            VisibleByDefault = true,
            Type = MapFieldType(field.Type)
        };
    }

    private static bool IsSimpleField(
        ReportDataField field)
    {
        return field.Type is not
            ReportDataType.Object
            and not ReportDataType.Array;
    }

    private static ReportFieldType MapFieldType(
        ReportDataType type)
    {
        return type switch
        {
            ReportDataType.Number =>
                ReportFieldType.Number,

            ReportDataType.Date or
            ReportDataType.DateTime =>
                ReportFieldType.Date,

            ReportDataType.Boolean =>
                ReportFieldType.Boolean,

            _ =>
                ReportFieldType.Text
        };
    }

    private static string ToLabel(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var chars =
            new List<char>();

        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 &&
                char.IsUpper(value[i]) &&
                !char.IsUpper(value[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(value[i]);
        }

        var result =
            new string(chars.ToArray());

        return char.ToUpperInvariant(result[0]) +
               result[1..];
    }
}