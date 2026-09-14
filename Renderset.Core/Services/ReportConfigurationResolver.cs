using Renderset.Core.Blocks;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Resolved;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Renderset.Core.Services;

public sealed class ReportConfigurationResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public ResolvedReportDefinition Resolve(
        ReportDefinition definition,
        ReportConfiguration? configuration = null,
        IReadOnlyCollection<ReportBlock>? bodyBlocks = null)
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

        var sections = ResolveSections(
            definition,
            configuration);

        var body = ResolveBody(
            sections,
            configuration,
            bodyBlocks ?? []);

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
            Body = body,
            Footer = footer
        };
    }

    private static List<ResolvedReportSection> ResolveSections(
        ReportDefinition definition,
        ReportConfiguration? configuration)
    {
        var sections = definition.Sections
            .Select(section => ResolveSection(section, configuration))
            .ToList();

        if (configuration is not null)
        {
            var definitionIds = definition.Sections
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var customSections = configuration.Sections
                .Where(x => !definitionIds.Contains(x.SectionId))
                .Select(ResolveCustomSection);

            sections.AddRange(customSections);
        }

        return sections
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ResolvedReportSection ResolveCustomSection(
        ReportSectionConfiguration configuration)
    {
        return new ResolvedReportSection
        {
            Id = configuration.SectionId,
            Name = configuration.NameOverride ?? "Nueva sección",
            Visible = configuration.Visible ?? true,
            Layout = configuration.Layout ?? ReportSectionLayout.List,
            Order = configuration.Order ?? int.MaxValue,
            Fields = [],
            Table = null
        };
    }

    private static List<ResolvedReportBodyItem> ResolveBody(
        IReadOnlyCollection<ResolvedReportSection> sections,
        ReportConfiguration? configuration,
        IReadOnlyCollection<ReportBlock> bodyBlocks)
    {
        if (configuration is null || configuration.Body.Count == 0)
        {
            return sections
                .OrderBy(x => x.Order)
                .Select(section => new ResolvedReportBodyItem
                {
                    Id = $"section-{section.Id}",
                    Type = ReportBodyItemType.Section,
                    Order = section.Order,
                    Section = section
                })
                .ToList();
        }

        var result = new List<ResolvedReportBodyItem>();

        foreach (var item in configuration.Body.OrderBy(x => x.Order))
        {
            switch (item.Type)
            {
                case ReportBodyItemType.Section:
                    {
                        if (string.IsNullOrWhiteSpace(item.SectionId))
                            continue;

                        var section = sections.FirstOrDefault(x =>
                            string.Equals(
                                x.Id,
                                item.SectionId,
                                StringComparison.OrdinalIgnoreCase));

                        if (section is null)
                            continue;

                        result.Add(new ResolvedReportBodyItem
                        {
                            Id = item.Id,
                            Type = ReportBodyItemType.Section,
                            Order = item.Order,
                            Section = section
                        });

                        break;
                    }

                case ReportBodyItemType.Block:
                    {
                        var block = ResolveBodyBlock(item, bodyBlocks);

                        if (block is null)
                            continue;

                        result.Add(new ResolvedReportBodyItem
                        {
                            Id = item.Id,
                            Type = ReportBodyItemType.Block,
                            Order = item.Order,
                            Block = block
                        });

                        break;
                    }
            }
        }

        // Compatibilidad con presets anteriores: si una sección aún no figura
        // explícitamente en Body, la conservamos al final en lugar de perderla.
        var includedSectionIds = result
            .Where(x => x.Section is not null)
            .Select(x => x.Section!.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextOrder = result.Count == 0
            ? 10
            : result.Max(x => x.Order) + 10;

        foreach (var section in sections
                     .Where(x => !includedSectionIds.Contains(x.Id))
                     .OrderBy(x => x.Order))
        {
            result.Add(new ResolvedReportBodyItem
            {
                Id = $"section-{section.Id}",
                Type = ReportBodyItemType.Section,
                Order = nextOrder,
                Section = section
            });

            nextOrder += 10;
        }

        return result
            .OrderBy(x => x.Order)
            .ToList();
    }

    private static ResolvedReportBlock? ResolveBodyBlock(
        ReportBodyItemConfiguration item,
        IReadOnlyCollection<ReportBlock> bodyBlocks)
    {
        if (item.LocalBlock is not null)
        {
            return ResolveBlock(
                item.Id,
                item.LocalBlock.Name,
                item.LocalBlock.Type,
                item.LocalBlock.ConfigurationJson);
        }

        if (string.IsNullOrWhiteSpace(item.BlockId))
            return null;

        var source = bodyBlocks.FirstOrDefault(x =>
            string.Equals(
                x.Id,
                item.BlockId,
                StringComparison.OrdinalIgnoreCase));

        if (source is null)
            return null;

        return ResolveBlock(
            source.Id,
            source.Name,
            source.Type,
            source.ConfigurationJson);
    }

    private static ResolvedReportBlock ResolveBlock(
        string id,
        string? name,
        ReportBlockType type,
        string configurationJson)
    {
        return new ResolvedReportBlock
        {
            Id = id,
            Name = name,
            Type = type,
            Text = type == ReportBlockType.Text
                ? DeserializeTextConfiguration(configurationJson)
                : null
        };
    }

    private static ReportTextBlockConfiguration DeserializeTextConfiguration(
        string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return new ReportTextBlockConfiguration();

        try
        {
            return JsonSerializer.Deserialize<ReportTextBlockConfiguration>(
                       configurationJson,
                       JsonOptions)
                   ?? new ReportTextBlockConfiguration();
        }
        catch (JsonException)
        {
            return new ReportTextBlockConfiguration();
        }
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
            Name = config?.NameOverride ?? definition.Name,
            Visible = config?.Visible ?? definition.VisibleByDefault,
            Layout = definition.Table is not null
                ? ReportSectionLayout.List
                : config?.Layout ?? ReportSectionLayout.List,
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
