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
                    order: order,
                    prefix: string.Empty));

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
                        order: order,
                        prefix: string.Empty));

                order += 10;

                continue;
            }

            if (field.Type == ReportDataType.Array)
            {
                sections.Add(
                    CreateArraySection(
                        field,
                        order,
                        prefix: string.Empty));

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


    /// <summary>
    /// Una colección se convierte en tabla cuando todos sus hijos son
    /// escalares, y en sección repetida cuando alguno es a su vez una
    /// colección o un objeto.
    ///
    /// Es la diferencia entre "líneas de factura" (tabla) y "albaranes, cada
    /// uno con su cabecera y sus líneas" (sección repetida). Una tabla no
    /// puede contener otra tabla; una sección sí.
    /// </summary>
    private static ReportSectionDefinition CreateArraySection(
        ReportDataField arrayField,
        int order,
        string prefix)
    {
        var nested =
            arrayField.Children
                .Where(x => !IsSimpleField(x))
                .ToList();

        if (nested.Count == 0)
        {
            return CreateTableSection(
                arrayField,
                order,
                prefix);
        }

        var scalars =
            arrayField.Children
                .Where(IsSimpleField)
                .ToList();

        // Dentro de la sección repetida, las rutas van relativas al elemento.
        var childPrefix = arrayField.Path;

        var childSections = new List<ReportSectionDefinition>();
        var childOrder = 10;

        foreach (var child in nested)
        {
            childSections.Add(
                child.Type == ReportDataType.Array
                    ? CreateArraySection(child, childOrder, childPrefix)
                    : CreateFieldsSection(
                        id: child.Path,
                        name: ToLabel(child.Name),
                        fields: child.Children,
                        order: childOrder,
                        prefix: childPrefix));

            childOrder += 10;
        }

        return new ReportSectionDefinition
        {
            Id = arrayField.Path,
            Name = ToLabel(arrayField.Name),
            Order = order,
            VisibleByDefault = true,

            // Relativo al contexto del padre: si el padre ya repite, sólo el
            // último tramo de la ruta.
            DataPath = Relative(arrayField.Path, prefix),

            Fields =
                scalars
                    .Select((field, index) =>
                        CreateFieldDefinition(
                            field,
                            (index + 1) * 10,
                            childPrefix))
                    .ToList(),

            Sections = childSections
        };
    }


    private static ReportSectionDefinition CreateFieldsSection(
        string id,
        string name,
        IReadOnlyCollection<ReportDataField> fields,
        int order,
        string prefix)
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
                    .Select((field, index) =>
                        CreateFieldDefinition(
                            field,
                            (index + 1) * 10,
                            prefix))
                    .ToList()
        };
    }


    private static ReportSectionDefinition CreateTableSection(
        ReportDataField arrayField,
        int order,
        string prefix)
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
                DataPath = Relative(arrayField.Path, prefix),

                Columns =
                    arrayField.Children
                        .Where(IsSimpleField)
                        .Select((field, index) =>
                            CreateColumnDefinition(
                                field,
                                (index + 1) * 10))
                        .ToList()
            }
        };
    }


    private static ReportFieldDefinition CreateFieldDefinition(
        ReportDataField field,
        int order,
        string prefix)
    {
        return new ReportFieldDefinition
        {
            // El Id se mantiene absoluto: es la identidad del campo en la
            // configuración y en las claves del diccionario, y tiene que ser
            // única en todo el report.
            Id = field.Path,
            Label = ToLabel(field.Name),

            // El DataPath es relativo al contexto donde se pinta.
            DataPath = Relative(field.Path, prefix),

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


    /// <summary>
    /// Quita el prefijo del contexto:
    /// "albaran.lines2" dentro de "albaran" queda en "lines2".
    /// </summary>
    private static string Relative(
        string path,
        string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return path;

        return path.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
            ? path[(prefix.Length + 1)..]
            : path;
    }


    private static bool IsSimpleField(
        ReportDataField field) =>
        field.Type != ReportDataType.Object &&
        field.Type != ReportDataType.Array;


    private static string ToLabel(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var builder = new System.Text.StringBuilder();

        foreach (var character in name)
        {
            if (char.IsUpper(character) && builder.Length > 0)
                builder.Append(' ');

            builder.Append(character);
        }

        var label = builder.ToString().Replace('_', ' ').Trim();

        return char.ToUpperInvariant(label[0]) + label[1..];
    }


    private static ReportFieldType MapFieldType(
        ReportDataType type) =>
        type switch
        {
            ReportDataType.Number => ReportFieldType.Number,
            ReportDataType.Boolean => ReportFieldType.Boolean,
            ReportDataType.Date => ReportFieldType.Date,
            ReportDataType.DateTime => ReportFieldType.Date,
            _ => ReportFieldType.Text
        };
}
