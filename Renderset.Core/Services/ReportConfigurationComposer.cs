using System.Text.Json;
using System.Text.Json.Serialization;
using Renderset.Core.Blocks;
using Renderset.Core.Configurations;

namespace Renderset.Core.Services;

public sealed class ReportConfigurationComposer
    : IReportConfigurationComposer
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

    public ReportConfiguration Compose(
        ReportConfiguration configuration,
        ReportBlock? headerBlock = null,
        ReportBlock? footerBlock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var effective = Clone(configuration);

        ApplyHeaderBlock(effective, headerBlock);
        ApplyFooterBlock(effective, footerBlock);

        return effective;
    }

    private static void ApplyHeaderBlock(
        ReportConfiguration effective,
        ReportBlock? block)
    {
        if (block is null)
            return;

        if (block.Type != ReportBlockType.Header)
        {
            throw new InvalidOperationException(
                $"El bloque '{block.Id}' es de tipo '{block.Type}' y no puede usarse como cabecera.");
        }

        effective.Header ??= new ReportHeaderConfiguration();

        if (!string.IsNullOrWhiteSpace(effective.Header.BlockId) &&
            !string.Equals(
                effective.Header.BlockId,
                block.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var blockConfiguration =
            JsonSerializer.Deserialize<ReportHeaderConfiguration>(
                block.ConfigurationJson,
                JsonOptions);

        if (blockConfiguration is null)
            return;

        effective.Header.BlockId ??= block.Id;
        effective.Header.Visible ??= blockConfiguration.Visible;
        effective.Header.TitleKey ??= blockConfiguration.TitleKey;
        effective.Header.SubtitleKey ??= blockConfiguration.SubtitleKey;
        effective.Header.ShowLogo ??= blockConfiguration.ShowLogo;
        effective.Header.LogoUrl ??= blockConfiguration.LogoUrl;
    }

    private static void ApplyFooterBlock(
        ReportConfiguration effective,
        ReportBlock? block)
    {
        if (block is null)
            return;

        if (block.Type != ReportBlockType.Footer)
        {
            throw new InvalidOperationException(
                $"El bloque '{block.Id}' es de tipo '{block.Type}' y no puede usarse como pie de página.");
        }

        effective.Footer ??= new ReportFooterConfiguration();

        if (!string.IsNullOrWhiteSpace(effective.Footer.BlockId) &&
            !string.Equals(
                effective.Footer.BlockId,
                block.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var blockConfiguration =
            JsonSerializer.Deserialize<ReportFooterConfiguration>(
                block.ConfigurationJson,
                JsonOptions);

        if (blockConfiguration is null)
            return;

        effective.Footer.BlockId ??= block.Id;
        effective.Footer.Visible ??= blockConfiguration.Visible;
        effective.Footer.TextKey ??= blockConfiguration.TextKey;
        effective.Footer.ShowGenerationDate ??= blockConfiguration.ShowGenerationDate;
        effective.Footer.ShowPageNumber ??= blockConfiguration.ShowPageNumber;
    }

    private static ReportConfiguration Clone(
        ReportConfiguration source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);

        return JsonSerializer.Deserialize<ReportConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                "No se ha podido clonar la configuración del report.");
    }
}
