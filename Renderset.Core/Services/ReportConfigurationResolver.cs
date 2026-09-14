using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Resolved;

namespace Renderset.Core.Services;

public sealed class ReportConfigurationResolver
{
    public ResolvedReportDefinition Resolve(
        ReportDefinition definition,
        ReportConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (configuration is not null &&
            !string.Equals(
                configuration.ReportId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La configuración '{configuration.Id}' pertenece al report " +
                $"'{configuration.ReportId}' y no puede aplicarse a '{definition.Id}'.");
        }

        var header = ResolveHeader(
            definition.Header,
            configuration?.Header);

        var sections = definition.Sections
            .Select(section => ResolveSection(section, configuration))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var footer = ResolveFooter(
            definition.Footer,
            configuration?.Footer);

        return new ResolvedReportDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            Version = definition.Version,
            Header = header,
            Sections = sections,
            Footer = footer
        };
    }

    private static ResolvedReportHeader? ResolveHeader(
        ReportHeaderDefinition? definition,
        ReportHeaderConfiguration? configuration)
    {
        if (definition is null)
            return null;

        return new ResolvedReportHeader
        {
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Title = configuration?.TitleOverride ?? definition.Title,
            Subtitle = configuration?.SubtitleOverride ?? definition.Subtitle,
            ShowLogo = configuration?.ShowLogo ?? definition.AllowLogo,
            LogoUrl = configuration?.LogoUrl,
            Fields = definition.Fields
                .OrderBy(x => x.Order)
                .Select(field => new ResolvedReportField
                {
                    Id = field.Id,
                    Label = field.Label,
                    DataPath = field.DataPath,
                    Order = field.Order,
                    Visible = field.VisibleByDefault,
                    Type = field.Type
                })
                .ToList()
        };
    }

    private static ResolvedReportSection ResolveSection(
        ReportSectionDefinition definition,
        ReportConfiguration? configuration)
    {
        var config = configuration?
            .Sections
            .FirstOrDefault(x =>
                string.Equals(
                    x.SectionId,
                    definition.Id,
                    StringComparison.OrdinalIgnoreCase));

        var fields = definition.Fields
            .Select(field =>
                ResolveField(
                    field,
                    config?.Fields.FirstOrDefault(x =>
                        string.Equals(
                            x.FieldId,
                            field.Id,
                            StringComparison.OrdinalIgnoreCase))))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var table = definition.Table is null
            ? null
            : ResolveTable(definition.Table, config?.Table);

        return new ResolvedReportSection
        {
            Id = definition.Id,
            Name = definition.Name,
            Visible = config?.Visible ?? definition.VisibleByDefault,
            Layout = config?.Layout ?? ReportSectionLayout.List,
            Order = config?.Order ?? definition.Order,
            Fields = fields,
            Table = table
        };
    }

    private static ResolvedReportFooter? ResolveFooter(
        ReportFooterDefinition? definition,
        ReportFooterConfiguration? configuration)
    {
        if (definition is null)
            return null;

        return new ResolvedReportFooter
        {
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Text = configuration?.TextOverride ?? definition.Text,
            ShowGenerationDate = configuration?.ShowGenerationDate ?? definition.ShowGenerationDate,
            ShowPageNumber = configuration?.ShowPageNumber ?? false
        };
    }

    private static ResolvedReportField ResolveField(
        ReportFieldDefinition definition,
        ReportFieldConfiguration? configuration)
    {
        return new ResolvedReportField
        {
            Id = definition.Id,
            Label = configuration?.LabelOverride ?? definition.Label,
            DataPath = definition.DataPath,
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Order = configuration?.Order ?? definition.Order,
            Type = definition.Type
        };
    }

    private static ResolvedReportTable ResolveTable(
        ReportTableDefinition definition,
        ReportTableConfiguration? configuration)
    {
        var columns = definition.Columns
            .Select(column =>
                ResolveColumn(
                    column,
                    configuration?.Columns.FirstOrDefault(x =>
                        string.Equals(
                            x.ColumnId,
                            column.Id,
                            StringComparison.OrdinalIgnoreCase))))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResolvedReportTable
        {
            Id = definition.Id,
            Name = definition.Name,
            DataPath = definition.DataPath,
            Columns = columns
        };
    }

    private static ResolvedReportColumn ResolveColumn(
        ReportColumnDefinition definition,
        ReportColumnConfiguration? configuration)
    {
        return new ResolvedReportColumn
        {
            Id = definition.Id,
            Label = configuration?.LabelOverride ?? definition.Label,
            DataPath = definition.DataPath,
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Order = configuration?.Order ?? definition.Order,
            Width = configuration?.Width ?? definition.Width,
            Type = definition.Type
        };
    }
}
