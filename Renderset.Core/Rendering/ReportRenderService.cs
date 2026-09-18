using System.Text.Json;
using Renderset.Core.Blocks;
using Renderset.Core.Data;
using Renderset.Core.Localization;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Services;
using Renderset.Core.Variables;

namespace Renderset.Core.Rendering;

/// <summary>
/// Orquesta la generación de un documento: report, preset, bloques,
/// diccionario, variables, resolver y render.
///
/// Esta secuencia existía sólo dentro del diseñador, repartida entre
/// ReportDesigner y ReportEditor. Tenerla aquí es lo que permite que la API
/// emita exactamente lo mismo que enseña el preview, en lugar de una segunda
/// implementación que se parece.
/// </summary>
public sealed class ReportRenderService
    : IReportRenderService
{
    private readonly IReportRepository _reports;
    private readonly IReportPresetRepository _presets;
    private readonly IReportPresetProvider _presetProvider;
    private readonly IReportBlockRepository _blocks;
    private readonly IReportVariableValues _variableValues;
    private readonly IReportTextCatalogFactory _catalogFactory;
    private readonly IReportConfigurationComposer _composer;
    private readonly IReportDocumentRenderer _documentRenderer;
    private readonly IRenderedDocumentRepository _documents;

    private readonly ReportConfigurationResolver _resolver = new();

    private readonly IReportDataAccessor _dataAccessor =
        new JsonReportDataAccessor();

    public ReportRenderService(
        IReportRepository reports,
        IReportPresetRepository presets,
        IReportPresetProvider presetProvider,
        IReportBlockRepository blocks,
        IReportVariableValues variableValues,
        IReportTextCatalogFactory catalogFactory,
        IReportConfigurationComposer composer,
        IReportDocumentRenderer documentRenderer,
        IRenderedDocumentRepository documents)
    {
        _reports = reports;
        _presets = presets;
        _presetProvider = presetProvider;
        _blocks = blocks;
        _variableValues = variableValues;
        _catalogFactory = catalogFactory;
        _composer = composer;
        _documentRenderer = documentRenderer;
        _documents = documents;
    }

    public async Task<RenderResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var validation = Validate(request);

        if (validation is not null)
            return validation;

        var report =
            await _reports.GetByIdAsync(
                tenantId,
                request.ReportId,
                cancellationToken);

        if (report is null)
        {
            return RenderResult.Failure(
                "report.not_found",
                $"No existe el report '{request.ReportId}' en el tenant '{tenantId}'.",
                path: "reportId");
        }

        var preset =
            await ResolvePresetAsync(
                tenantId,
                request,
                cancellationToken);

        if (preset is null)
        {
            return RenderResult.Failure(
                "preset.not_found",
                string.IsNullOrWhiteSpace(request.PresetId)
                    ? $"No hay preset asignado al report '{request.ReportId}' para ese contexto, " +
                      "ni preset por defecto."
                    : $"No existe el preset '{request.PresetId}'.",
                path: string.IsNullOrWhiteSpace(request.PresetId)
                    ? "context"
                    : "presetId");
        }

        // El preset se pide por id o se resuelve por assignment, y en ninguno
        // de los dos casos está garantizado que sea de este report.
        if (!string.Equals(
                preset.Configuration.ReportId,
                report.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return RenderResult.Failure(
                "preset.report_mismatch",
                $"El preset '{preset.Id}' pertenece al report " +
                $"'{preset.Configuration.ReportId}'.",
                path: "presetId",
                expected: report.Id,
                actual: preset.Configuration.ReportId);
        }

        // Los valores llegan ya resueltos para el idioma del documento: una
        // variable marcada como traducible sale del diccionario común y no
        // de su valor literal.
        var variableValues =
            await _variableValues.GetAsync(
                tenantId,
                request.Context.Culture,
                cancellationToken);

        var catalog =
            await BuildCatalogAsync(
                tenantId,
                request,
                preset,
                variableValues,
                cancellationToken);

        var headerBlock =
            await GetBlockAsync(
                tenantId,
                preset.Configuration.Header?.BlockId,
                variableValues,
                cancellationToken);

        var footerBlock =
            await GetBlockAsync(
                tenantId,
                preset.Configuration.Footer?.BlockId,
                variableValues,
                cancellationToken);

        var bodyBlocks =
            await GetBodyBlocksAsync(
                tenantId,
                variableValues,
                cancellationToken);

        var effectiveConfiguration =
            _composer.Compose(
                preset.Configuration,
                headerBlock,
                footerBlock);

        var resolved =
            _resolver.Resolve(
                report.Definition,
                effectiveConfiguration,
                bodyBlocks,
                catalog);

        var data = request.Data!.Value;

        var html =
            await _documentRenderer.RenderHtmlAsync(
                resolved,
                data,
                preset.Theme,
                resolved.Name,
                request.Output.IncludeToolbar,
                request.Output.IncludeDocumentData,
                cancellationToken);

        var document =
            new RenderedDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                ReportId = report.Id,
                PresetId = preset.Id,
                PresetVersion = preset.Version,
                Culture = catalog.Culture,
                FileName = BuildFileName(
                    request,
                    report,
                    data,
                    variableValues),
                Format = RenderFormat.Html,
                Content = html,
                CreatedAtUtc = DateTime.UtcNow
            };

        await _documents.SaveAsync(
            tenantId,
            document,
            cancellationToken);

        return RenderResult.Success(document);
    }

    private static RenderResult? Validate(
        RenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId))
        {
            return RenderResult.Failure(
                "request.report_required",
                "Falta el reportId.",
                path: "reportId");
        }

        if (request.Output.Format != RenderFormat.Html)
        {
            return RenderResult.Failure(
                "output.format_not_supported",
                "De momento sólo se emite HTML. El PDF se generará sobre este " +
                "mismo documento cuando esté el motor de conversión.",
                path: "output.format",
                expected: nameof(RenderFormat.Html),
                actual: request.Output.Format.ToString());
        }

        if (request.Output.BatchMode != RenderBatchMode.Single)
        {
            return RenderResult.Failure(
                "output.batch_not_supported",
                "De momento se emite un documento por petición.",
                path: "output.batchMode",
                expected: nameof(RenderBatchMode.Single),
                actual: request.Output.BatchMode.ToString());
        }

        return request.ResolveMode() switch
        {
            RenderRequestMode.Hierarchical => null,

            RenderRequestMode.None => RenderResult.Failure(
                "request.data_required",
                "Hay que enviar 'data' con el documento ya jerárquico.",
                path: "data"),

            RenderRequestMode.Ambiguous => RenderResult.Failure(
                "request.data_ambiguous",
                "Se han enviado 'data' y 'rows' a la vez. Sólo uno de los dos.",
                path: "data"),

            // El HierarchyBuilder está hecho, pero los mappings todavía no se
            // guardan en ningún sitio, así que MappingId no se puede resolver.
            _ => RenderResult.Failure(
                "request.tabular_not_supported",
                "Todavía no se admiten datos tabulares: falta la persistencia " +
                "de mappings. Envía 'data' ya jerárquico.",
                path: "rows")
        };
    }

    private async Task<ReportPreset?> ResolvePresetAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PresetId))
        {
            return await _presets.GetByIdAsync(
                tenantId,
                request.PresetId,
                cancellationToken);
        }

        return await _presetProvider.GetPresetAsync(
            tenantId,
            request.ReportId,
            request.Context.ContextType,
            request.Context.ContextKey,
            cancellationToken);
    }

    /// <summary>
    /// El catálogo llega con plantillas ("CIF: {{company.cif}}"), porque el
    /// diccionario guarda el texto traducible tal cual se edita. Las
    /// variables se sustituyen aquí, justo antes de resolver, igual que hace
    /// el workspace del diseñador al construir su catálogo.
    /// </summary>
    private async Task<ReportTextCatalog> BuildCatalogAsync(
        string tenantId,
        RenderRequest request,
        ReportPreset preset,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var catalog =
            await _catalogFactory.CreateAsync(
                tenantId,
                request.ReportId,
                preset.Id,
                request.Context.Culture,
                cancellationToken);

        if (variables.Count == 0)
            return catalog;

        var entries = new Dictionary<string, string>(
            catalog.Entries,
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in entries.Keys.ToList())
        {
            if (!ReportVariableTemplate.HasTokens(entries[key]))
                continue;

            entries[key] =
                ReportVariableTemplate.Apply(
                    entries[key],
                    variables);
        }

        return new ReportTextCatalog(
            entries,
            catalog.Culture);
    }

    private async Task<ReportBlock?> GetBlockAsync(
        string tenantId,
        string? blockId,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            return null;

        var block =
            await _blocks.GetByIdAsync(
                tenantId,
                blockId,
                cancellationToken);

        return block is null
            ? null
            : WithResolvedVariables(block, variables);
    }

    private async Task<IReadOnlyCollection<ReportBlock>> GetBodyBlocksAsync(
        string tenantId,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var blocks =
            await _blocks.GetAllAsync(
                tenantId,
                cancellationToken);

        return blocks
            .Where(x => x.Type is
                ReportBlockType.Text or
                ReportBlockType.Image or
                ReportBlockType.Fields)
            .Select(block => WithResolvedVariables(block, variables))
            .ToList();
    }

    /// <summary>
    /// Mismo tratamiento que da la API al servir un bloque resuelto: la
    /// configuración del bloque lleva literales (la URL del logo, por
    /// ejemplo) que pueden apuntar a una variable del tenant.
    /// </summary>
    private static ReportBlock WithResolvedVariables(
        ReportBlock block,
        IReadOnlyDictionary<string, string> variables)
    {
        if (!ReportVariableTemplate.HasTokens(block.ConfigurationJson))
            return block;

        return new ReportBlock
        {
            Id = block.Id,
            Name = block.Name,
            Type = block.Type,
            ConfigurationJson =
                ReportVariableTemplate.Apply(
                    block.ConfigurationJson,
                    variables)
        };
    }

    /// <summary>
    /// El nombre admite variables del tenant y también rutas del documento:
    /// "factura-{{data.numeroFactura}}.html".
    /// </summary>
    private string BuildFileName(
        RenderRequest request,
        Report report,
        JsonElement data,
        IReadOnlyDictionary<string, string> variables)
    {
        var template =
            string.IsNullOrWhiteSpace(request.Output.FileName)
                ? $"{report.Id}.html"
                : request.Output.FileName!;

        var name =
            ReportVariableTemplate.Apply(
                template,
                variables,
                onMissing: key =>
                    key.StartsWith("data.", StringComparison.OrdinalIgnoreCase) &&
                    _dataAccessor.TryGetValue(data, key[5..], out var value)
                        ? value?.ToString() ?? string.Empty
                        : string.Empty);

        name = Sanitize(name);

        return string.IsNullOrWhiteSpace(name)
            ? $"{report.Id}.html"
            : name;
    }

    private static string Sanitize(
        string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();

        var cleaned =
            new string(
                fileName
                    .Where(c => !invalid.Contains(c))
                    .ToArray())
                .Trim();

        return cleaned.Length > 180
            ? cleaned[..180]
            : cleaned;
    }
}
