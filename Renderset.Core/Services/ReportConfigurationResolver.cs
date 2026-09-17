using Renderset.Core.Blocks;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Localization;
using Renderset.Core.Resolved;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Renderset.Core.Services;

public sealed class ReportConfigurationResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
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
        IReadOnlyCollection<ReportBlock>? bodyBlocks = null,
        ReportTextCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var texts = catalog ?? ReportTextCatalog.Empty;

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
            texts,
            definition.Header,
            configuration?.Header);

        var sections = ResolveSections(
            texts,
            definition,
            configuration);

        var body = ResolveBody(
            texts,
            sections,
            configuration,
            bodyBlocks ?? []);

        var footer = ResolveFooter(
            texts,
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
        ReportTextCatalog texts,
        ReportDefinition definition,
        ReportConfiguration? configuration)
    {
        var sections = definition.Sections
            .Select(section => ResolveSection(
                texts,
                section,
                definition,
                configuration))
            .ToList();

        if (configuration is not null)
        {
            var definitionSectionIds = definition.Sections
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var customSections = configuration.Sections
                .Where(x => !definitionSectionIds.Contains(x.SectionId))
                .Select(sectionConfiguration => ResolveCustomSection(
                    texts,
                    sectionConfiguration,
                    definition,
                    configuration));

            sections.AddRange(customSections);
        }

        return sections
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ResolvedReportSection ResolveCustomSection(
        ReportTextCatalog texts,
        ReportSectionConfiguration configuration,
        ReportDefinition definition,
        ReportConfiguration reportConfiguration)
    {
        var fields = ResolvePlacedFields(
            texts,
            sectionId: configuration.SectionId,
            definition,
            reportConfiguration,
            sectionConfiguration: configuration);

        return new ResolvedReportSection
        {
            Id = configuration.SectionId,
            Name = texts.Resolve(
                configuration.NameKey,
                texts.Resolve(
                    ReportTextKeys.Section(configuration.SectionId),
                    configuration.SectionId)),
            Visible = configuration.Visible ?? true,
            Layout = configuration.Layout ?? ReportSectionLayout.List,
            Order = configuration.Order ?? 0,
            Fields = fields,
            Table = null,
            ShowName = configuration?.ShowName ?? true,
        };
    }

    private static ResolvedReportSection ResolveSection(
        ReportTextCatalog texts,
        ReportSectionDefinition definition,
        ReportDefinition reportDefinition,
        ReportConfiguration? configuration)
    {
        var sectionConfiguration = configuration?
            .Sections
            .FirstOrDefault(x =>
                string.Equals(
                    x.SectionId,
                    definition.Id,
                    StringComparison.OrdinalIgnoreCase));

        var fields = ResolveDefinitionSectionFields(
            texts,
            definition,
            reportDefinition,
            configuration,
            sectionConfiguration);

        var table = definition.Table is null
            ? null
            : ResolveTable(
                texts,
                definition.Table,
                sectionConfiguration?.Table);

        var childSections =
            definition.Sections
                .Select(child => ResolveSection(
                    texts,
                    child,
                    reportDefinition,
                    // Las subsecciones se configuran dentro de su padre, no
                    // en la lista plana del report.
                    ChildConfiguration(sectionConfiguration)))
                .OrderBy(x => x.Order)
                .ToList();

        return new ResolvedReportSection
        {
            Id = definition.Id,
            Name = texts.Resolve(
                sectionConfiguration?.NameKey,
                texts.Resolve(
                    ReportTextKeys.Section(definition.Id),
                    definition.Name)),
            Visible = sectionConfiguration?.Visible ?? definition.VisibleByDefault,
            Layout = definition.Table is not null
                ? ReportSectionLayout.List
                : sectionConfiguration?.Layout ?? ReportSectionLayout.List,
            Order = sectionConfiguration?.Order ?? definition.Order,
            DataPath = definition.DataPath,
            Fields = fields,
            Table = table,
            Sections = childSections,
            ShowName = sectionConfiguration?.ShowName ?? true,
        };
    }

    /// <summary>
    /// Envuelve las subsecciones de una sección en una configuración
    /// temporal, para poder reutilizar ResolveSection tal cual.
    /// </summary>
    private static ReportConfiguration? ChildConfiguration(
        ReportSectionConfiguration? parent)
    {
        if (parent is null || parent.Sections.Count == 0)
            return null;

        return new ReportConfiguration
        {
            Id = "child",
            ReportId = "child",
            Name = "child",
            Sections = parent.Sections
        };
    }

    private static List<ResolvedReportField> ResolveDefinitionSectionFields(
        ReportTextCatalog texts,
        ReportSectionDefinition sectionDefinition,
        ReportDefinition reportDefinition,
        ReportConfiguration? configuration,
        ReportSectionConfiguration? sectionConfiguration)
    {
        if (configuration is null)
        {
            return sectionDefinition.Fields
                .Select(field => ResolveField(
                    texts,
                    field,
                    sectionConfiguration?.Fields.FirstOrDefault(x =>
                        string.Equals(
                            x.FieldId,
                            field.Id,
                            StringComparison.OrdinalIgnoreCase))))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var placementsByFieldId =
            configuration.FieldPlacements
                .GroupBy(
                    x => x.FieldId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(p => p.Order).First(),
                    StringComparer.OrdinalIgnoreCase);

        var fields =
            new List<ResolvedReportField>();

        foreach (var fieldDefinition in sectionDefinition.Fields)
        {
            var hasPlacement =
                placementsByFieldId.TryGetValue(
                    fieldDefinition.Id,
                    out var placement);

            if (hasPlacement &&
                placement is not null &&
                !string.Equals(
                    placement.SectionId,
                    sectionDefinition.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fieldConfiguration =
                sectionConfiguration?
                    .Fields
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.FieldId,
                            fieldDefinition.Id,
                            StringComparison.OrdinalIgnoreCase));

            var orderOverride =
                hasPlacement &&
                placement is not null &&
                string.Equals(
                    placement.SectionId,
                    sectionDefinition.Id,
                    StringComparison.OrdinalIgnoreCase)
                    ? placement.Order
                    : (int?)null;

            fields.Add(
                ResolveField(
                    texts,
                    fieldDefinition,
                    fieldConfiguration,
                    orderOverride));
        }

        var originalFieldIds =
            sectionDefinition.Fields
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var incomingPlacements =
            configuration.FieldPlacements
                .Where(x =>
                    string.Equals(
                        x.SectionId,
                        sectionDefinition.Id,
                        StringComparison.OrdinalIgnoreCase) &&
                    !originalFieldIds.Contains(x.FieldId))
                .OrderBy(x => x.Order)
                .ToList();

        foreach (var placement in incomingPlacements)
        {
            var fieldDefinition =
                FindFieldDefinition(
                    reportDefinition,
                    placement.FieldId);

            if (fieldDefinition is null)
                continue;

            var fieldConfiguration =
                sectionConfiguration?
                    .Fields
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.FieldId,
                            fieldDefinition.Id,
                            StringComparison.OrdinalIgnoreCase));

            fields.Add(
                ResolveField(
                    texts,
                    fieldDefinition,
                    fieldConfiguration,
                    placement.Order));
        }

        return fields
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ResolvedReportField> ResolvePlacedFields(
        ReportTextCatalog texts,
        string sectionId,
        ReportDefinition definition,
        ReportConfiguration reportConfiguration,
        ReportSectionConfiguration? sectionConfiguration)
    {
        return reportConfiguration.FieldPlacements
            .Where(x =>
                string.Equals(
                    x.SectionId,
                    sectionId,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Order)
            .Select(placement =>
            {
                var fieldDefinition =
                    FindFieldDefinition(
                        definition,
                        placement.FieldId);

                if (fieldDefinition is null)
                    return null;

                var fieldConfiguration =
                    sectionConfiguration?.Fields.FirstOrDefault(x =>
                        string.Equals(
                            x.FieldId,
                            fieldDefinition.Id,
                            StringComparison.OrdinalIgnoreCase));

                return ResolveField(
                    texts,
                    fieldDefinition,
                    fieldConfiguration,
                    placement.Order);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    private static ReportFieldDefinition? FindFieldDefinition(
        ReportDefinition definition,
        string fieldId)
    {
        return definition.Sections
            .SelectMany(x => x.Fields)
            .FirstOrDefault(x =>
                string.Equals(
                    x.Id,
                    fieldId,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static List<ResolvedReportBodyItem> ResolveBody(
        ReportTextCatalog texts,
        IReadOnlyCollection<ResolvedReportSection> sections,
        ReportConfiguration? configuration,
        IReadOnlyCollection<ReportBlock> bodyBlocks)
    {
        if (configuration is null || configuration.Body.Count == 0)
        {
            var fallback = sections
                .Where(x => x.Visible)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(section => new ResolvedReportBodyItem
                {
                    Id = $"section-{section.Id}",
                    Type = ReportBodyItemType.Section,
                    Order = section.Order,
                    Section = section
                })
                .ToList();

            ReportBodyLayout.Apply(fallback, null);

            return fallback;
        }

        var result = new List<ResolvedReportBodyItem>();

        foreach (var item in configuration.Body.OrderBy(x => x.Order))
        {
            switch (item.Type)
            {
                case ReportBodyItemType.Section:
                    ResolveBodySection(
                        item,
                        sections,
                        result);
                    break;

                case ReportBodyItemType.Block:
                    ResolveBodyBlock(
                        texts,
                        item,
                        bodyBlocks,
                        result);
                    break;
            }
        }

        AddMissingSectionsForBackwardCompatibility(
            sections,
            result);

        var ordered = result
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // El layout se calcula sobre la lista definitiva: las secciones que
        // entran por compatibilidad hacia atrás también tienen que caer en
        // una fila, y lo hacen en la suya propia al no traer banderas.
        ReportBodyLayout.Apply(ordered, configuration.Body);

        return ordered;
    }

    private static void ResolveBodySection(
        ReportBodyItemConfiguration item,
        IReadOnlyCollection<ResolvedReportSection> sections,
        List<ResolvedReportBodyItem> result)
    {
        if (string.IsNullOrWhiteSpace(item.SectionId))
            return;

        var section = sections.FirstOrDefault(x =>
            string.Equals(
                x.Id,
                item.SectionId,
                StringComparison.OrdinalIgnoreCase));

        if (section is null || !section.Visible)
            return;

        result.Add(new ResolvedReportBodyItem
        {
            Id = item.Id,
            Type = ReportBodyItemType.Section,
            Order = item.Order,
            Section = section
        });
    }

    private static void ResolveBodyBlock(
        ReportTextCatalog texts,
        ReportBodyItemConfiguration item,
        IReadOnlyCollection<ReportBlock> bodyBlocks,
        List<ResolvedReportBodyItem> result)
    {
        var block = ResolveBodyBlock(
            texts,
            item,
            bodyBlocks);

        if (block is null)
            return;

        result.Add(new ResolvedReportBodyItem
        {
            Id = item.Id,
            Type = ReportBodyItemType.Block,
            Order = item.Order,
            Block = block
        });
    }

    private static void AddMissingSectionsForBackwardCompatibility(
        IReadOnlyCollection<ResolvedReportSection> sections,
        List<ResolvedReportBodyItem> result)
    {
        var includedSectionIds = result
            .Where(x => x.Section is not null)
            .Select(x => x.Section!.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextOrder = result.Count == 0
            ? 10
            : result.Max(x => x.Order) + 10;

        foreach (var section in sections
                     .Where(x => x.Visible)
                     .Where(x => !includedSectionIds.Contains(x.Id))
                     .OrderBy(x => x.Order)
                     .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
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
    }

    private static ResolvedReportBlock? ResolveBodyBlock(
        ReportTextCatalog texts,
        ReportBodyItemConfiguration item,
        IReadOnlyCollection<ReportBlock> bodyBlocks)
    {
        if (item.LocalBlock is not null)
        {
            return ResolveBlock(
                texts,
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
            texts,
            source.Id,
            source.Name,
            source.Type,
            source.ConfigurationJson);
    }

    private static ResolvedReportBlock ResolveBlock(
        ReportTextCatalog texts,
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
                ? LocalizeTextBlock(
                    texts,
                    DeserializeTextConfiguration(configurationJson))
                : null
        };
    }

    private static ReportTextBlockConfiguration LocalizeTextBlock(
        ReportTextCatalog texts,
        ReportTextBlockConfiguration configuration)
    {
        return new ReportTextBlockConfiguration
        {
            TitleKey = configuration.TitleKey,
            TextKey = configuration.TextKey,
            Title = string.IsNullOrWhiteSpace(configuration.TitleKey)
                ? configuration.Title
                : texts.Resolve(
                    configuration.TitleKey,
                    configuration.Title ?? string.Empty),
            Text = texts.Resolve(
                configuration.TextKey,
                configuration.Text),
            Style = configuration.Style
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
        ReportTextCatalog texts,
        ReportHeaderDefinition? definition,
        ReportHeaderConfiguration? configuration)
    {
        if (definition is null)
            return null;

        return new ResolvedReportHeader
        {
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Title = texts.Resolve(
                configuration?.TitleKey,
                texts.Resolve(
                    ReportTextKeys.HeaderTitle,
                    definition.Title ?? string.Empty)),
            Subtitle = texts.Resolve(
                configuration?.SubtitleKey,
                texts.Resolve(
                    ReportTextKeys.HeaderSubtitle,
                    definition.Subtitle ?? string.Empty)),
            ShowLogo = configuration?.ShowLogo ?? definition.AllowLogo,
            LogoUrl = configuration?.LogoUrl,
            LogoMaxHeight = configuration?.LogoMaxHeight,
            Layout = configuration?.Layout ?? ReportHeaderLayout.LogoLeft,
            BackgroundColor = configuration?.BackgroundColor,
            TextColor = configuration?.TextColor,

            // Sin configurar se comporta como siempre: línea de separación.
            ShowDivider = configuration?.ShowDivider ?? true,

            Lines = ResolveHeaderLines(
                texts,
                configuration),

            Fields = definition.Fields
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(field => new ResolvedReportField
                {
                    Id = field.Id,
                    Label = texts.Resolve(
                        ReportTextKeys.Field(field.Id),
                        field.Label),
                    DataPath = field.DataPath,
                    Order = field.Order,
                    Visible = field.VisibleByDefault,
                    Type = field.Type
                })
                .ToList()
        };
    }

    /// <summary>
    /// Las líneas de datos sólo existen en configuración: la definición se
    /// infiere de los datos del documento y no sabe nada de la empresa que
    /// emite el informe.
    /// </summary>
    private static List<ResolvedReportHeaderLine> ResolveHeaderLines(
        ReportTextCatalog texts,
        ReportHeaderConfiguration? configuration)
    {
        if (configuration is null)
            return [];

        return configuration.Lines
            .OrderBy(x => x.Order)
            .Select(line => new ResolvedReportHeaderLine
            {
                Text = texts.Resolve(
                    line.TextKey,
                    string.Empty),
                Style = line.Style
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();
    }

    private static ResolvedReportFooter? ResolveFooter(
        ReportTextCatalog texts,
        ReportFooterDefinition? definition,
        ReportFooterConfiguration? configuration)
    {
        if (definition is null)
            return null;

        return new ResolvedReportFooter
        {
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Text = texts.Resolve(
                configuration?.TextKey,
                texts.Resolve(
                    ReportTextKeys.FooterText,
                    definition.Text ?? string.Empty)),
            ShowGenerationDate = configuration?.ShowGenerationDate ?? definition.ShowGenerationDate,
            ShowPageNumber = configuration?.ShowPageNumber ?? false
        };
    }

    private static ResolvedReportField ResolveField(
        ReportTextCatalog texts,
        ReportFieldDefinition definition,
        ReportFieldConfiguration? configuration,
        int? orderOverride = null)
    {
        return new ResolvedReportField
        {
            Id = definition.Id,
            Label = texts.Resolve(
                configuration?.LabelKey,
                texts.Resolve(
                    ReportTextKeys.Field(definition.Id),
                    definition.Label)),
            DataPath = definition.DataPath,
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Order = orderOverride ?? configuration?.Order ?? definition.Order,
            Type = definition.Type
        };
    }

    private static ResolvedReportTable ResolveTable(
        ReportTextCatalog texts,
        ReportTableDefinition definition,
        ReportTableConfiguration? configuration)
    {
        var columns = definition.Columns
            .Select(column =>
                ResolveColumn(
                    texts,
                    definition.Id,
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
        ReportTextCatalog texts,
        string tableId,
        ReportColumnDefinition definition,
        ReportColumnConfiguration? configuration)
    {
        return new ResolvedReportColumn
        {
            Id = definition.Id,
            Label = texts.Resolve(
                configuration?.LabelKey,
                texts.Resolve(
                    ReportTextKeys.Column(tableId, definition.Id),
                    definition.Label)),
            DataPath = definition.DataPath,
            Visible = configuration?.Visible ?? definition.VisibleByDefault,
            Order = configuration?.Order ?? definition.Order,
            Width = configuration?.Width ?? definition.Width,
            Type = definition.Type
        };
    }
}
