using System.Text.Json;
using Renderset.Core.Blocks;
using Renderset.Core.Data;
using Renderset.Core.Localization;
using Renderset.Core.Mapping;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Services;
using Renderset.Core.Themes;
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
    private readonly IHtmlAssetInliner? _assetInliner;
    private readonly IReportThemeRepository? _themes;
    private readonly IDataMappingRepository? _mappings;
    private readonly DocumentLinkOptions _links;

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
        IRenderedDocumentRepository documents,
        IHtmlAssetInliner? assetInliner = null,
        IReportThemeRepository? themes = null,
        IDataMappingRepository? mappings = null,
        DocumentLinkOptions? links = null)
    {
        _links = links ?? new DocumentLinkOptions();
        _mappings = mappings;
        _assetInliner = assetInliner;
        _themes = themes;
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

    /// <summary>
    /// Comprueba una petición de render sin emitir nada: el report existe,
    /// los datos (o las filas con su mapping) se pueden construir y hay un
    /// preset de ese report para el contexto. Es lo que hace
    /// POST /api/render/{tenantId}/validate.
    /// </summary>
    public async Task<RenderValidationResponse> ValidateAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var (prepared, failure) =
            await PrepareAsync(
                tenantId,
                request,
                cancellationToken);

        return new RenderValidationResponse
        {
            Valid = failure is null,
            ReportId = request.ReportId,
            PresetId = prepared?.Preset.Id,
            PresetVersion = prepared?.Preset.Version,
            Errors = failure?.Errors ?? []
        };
    }

    private sealed record PreparedRender(
        Report Report,
        JsonElement Data,
        ReportPreset Preset);

    /// <summary>
    /// Lo que va antes de pintar: petición, report, datos y preset. Lo
    /// comparten el render y la validación, para que validar diga
    /// exactamente lo mismo que diría el render.
    /// </summary>
    private async Task<(PreparedRender? Prepared, RenderResult? Failure)> PrepareAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);

        if (validation is not null)
            return (null, validation);

        var report =
            await _reports.GetByIdAsync(
                tenantId,
                request.ReportId,
                cancellationToken);

        if (report is null)
        {
            return (null, RenderResult.Failure(
                "report.not_found",
                $"No existe el report '{request.ReportId}' en el tenant '{tenantId}'.",
                path: "reportId"));
        }

        // Los datos se resuelven antes que el diseño: si las filas no encajan
        // con el mapping, es lo primero que tiene que saber quien integra.
        JsonElement data;

        if (request.ResolveMode() == RenderRequestMode.Tabular)
        {
            var tabular =
                await BuildTabularDataAsync(
                    tenantId,
                    report.Id,
                    request,
                    cancellationToken);

            if (tabular.Failure is not null)
                return (null, tabular.Failure);

            data = tabular.Data;
        }
        else
        {
            data = request.Data!.Value;
        }

        var preset =
            await ResolvePresetAsync(
                tenantId,
                request,
                data,
                cancellationToken);

        if (preset is null)
        {
            return (null, RenderResult.Failure(
                "preset.not_found",
                string.IsNullOrWhiteSpace(request.PresetId)
                    ? $"No hay preset asignado al report '{request.ReportId}' para ese contexto, " +
                      "ni preset por defecto."
                    : $"No existe el preset '{request.PresetId}'.",
                path: string.IsNullOrWhiteSpace(request.PresetId)
                    ? "context"
                    : "presetId"));
        }

        // El preset se pide por id o se resuelve por assignment, y en ninguno
        // de los dos casos está garantizado que sea de este report.
        if (!string.Equals(
                preset.Configuration.ReportId,
                report.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, RenderResult.Failure(
                "preset.report_mismatch",
                $"El preset '{preset.Id}' pertenece al report " +
                $"'{preset.Configuration.ReportId}'.",
                path: "presetId",
                expected: report.Id,
                actual: preset.Configuration.ReportId));
        }

        return (new PreparedRender(report, data, preset), null);
    }

    public async Task<RenderResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var (prepared, failure) =
            await PrepareAsync(
                tenantId,
                request,
                cancellationToken);

        if (failure is not null)
            return failure;

        var report = prepared!.Report;
        var data = prepared.Data;
        var preset = prepared.Preset;


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

        var theme =
            await ResolveThemeAsync(
                tenantId,
                preset,
                cancellationToken);

        // El id se decide antes de pintar: el QR de la cabecera puede llevar
        // el enlace del propio documento.
        var documentId = Guid.NewGuid().ToString("N");

        var html =
            await _documentRenderer.RenderHtmlAsync(
                resolved,
                data,
                theme,
                resolved.Name,
                request.Output.IncludeDocumentData,
                new ReportDocumentContext
                {
                    DocumentId = documentId,

                    // Público si está configurado (lo abre cualquiera que
                    // lea el QR); si no, el visor de Web con sesión.
                    DocumentUrl = _links.DocumentUrl(tenantId, documentId),
                    ViewerUrl = _links.DocumentUrl(documentId),
                    PdfUrl = _links.PdfUrl(tenantId, documentId)
                },
                cancellationToken);

        // Las imágenes se incrustan antes de guardar: el documento emitido
        // tiene que ser autosuficiente para no cambiar si cambia la URL del
        // logo, y para que el PDF no dependa de la red.
        if (_assetInliner is not null)
        {
            html =
                await _assetInliner.InlineAsync(
                    html,
                    cancellationToken);
        }

        var document =
            new RenderedDocument
            {
                Id = documentId,
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

        // Pdf y Html emiten lo mismo: el documento se guarda siempre en HTML
        // y el PDF se deriva de él (al momento si se pide Pdf, o la primera
        // vez que alguien lo descarga). Así los dos son siempre idénticos.
        if (request.Output.Format is not (RenderFormat.Html or RenderFormat.Pdf))
        {
            return RenderResult.Failure(
                "output.format_not_supported",
                "Formato no soportado.",
                path: "output.format",
                expected: "Html | Pdf",
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
                "Hay que enviar 'data' (documento jerárquico) o 'rows' (filas planas con un mapping configurado).",
                path: "data"),

            RenderRequestMode.Ambiguous => RenderResult.Failure(
                "request.data_ambiguous",
                "Se han enviado 'data' y 'rows' a la vez. Sólo uno de los dos.",
                path: "data"),

            // Las filas se validan contra el mapping más adelante, cuando ya
            // se sabe qué mapping toca.
            _ => null
        };
    }

    /// <summary>
    /// Por id, o por assignment. El assignment puede salir del contexto de
    /// la petición o de los propios datos (un tipo de contexto que es la
    /// ruta de un campo, como "cliente.codigo").
    /// </summary>
    private async Task<ReportPreset?> ResolvePresetAsync(
        string tenantId,
        RenderRequest request,
        JsonElement data,
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
            data,
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

    /// <summary>
    /// Un preset con ThemeId se pinta con el tema de empresa vivo. Si ese
    /// tema ya no existe, con la copia que guarda el preset: un tema borrado
    /// no puede dejar de emitir documentos.
    /// </summary>
    private async Task<ReportTheme> ResolveThemeAsync(
        string tenantId,
        ReportPreset preset,
        CancellationToken cancellationToken)
    {
        var themeId = preset.Configuration.ThemeId;

        if (_themes is null || string.IsNullOrWhiteSpace(themeId))
            return preset.Theme;

        var stored =
            await _themes.GetByIdAsync(
                tenantId,
                themeId,
                cancellationToken);

        return stored?.Theme ?? preset.Theme;
    }

    /// <summary>
    /// Filas planas → documento, con el mapping indicado en la petición o,
    /// si no viene, con el asignado al report. Una petición emite un
    /// documento: si las filas producen varios, casi siempre es que la
    /// consulta trae más de un documento o que la clave raíz está mal.
    /// </summary>
    private async Task<(JsonElement Data, RenderResult? Failure)> BuildTabularDataAsync(
        string tenantId,
        string reportId,
        RenderRequest request,
        CancellationToken cancellationToken)
    {
        if (_mappings is null)
        {
            return (default, RenderResult.Failure(
                "request.tabular_not_supported",
                "Este servidor no tiene configurado el almacén de mappings.",
                path: "rows"));
        }

        var mapping =
            string.IsNullOrWhiteSpace(request.MappingId)
                ? await _mappings.GetForReportAsync(
                    tenantId,
                    reportId,
                    cancellationToken)
                : await _mappings.GetByIdAsync(
                    tenantId,
                    request.MappingId,
                    cancellationToken);

        if (mapping is null)
        {
            return (default, string.IsNullOrWhiteSpace(request.MappingId)
                ? RenderResult.Failure(
                    "mapping.not_configured",
                    $"El report '{reportId}' no tiene mapping para filas planas. " +
                    "Configúralo en Reports → Datos tabulares, o indica 'mappingId'.",
                    path: "mappingId")
                : RenderResult.Failure(
                    "mapping.not_found",
                    $"No existe el mapping '{request.MappingId}'.",
                    path: "mappingId"));
        }

        var rows = request.Rows!;

        var errors =
            TabularDocuments.Validate(
                rows,
                mapping);

        if (errors.Count > 0)
        {
            return (default, new RenderResult
            {
                Errors = errors
                    .Select(message => new RenderValidationError
                    {
                        Code = "rows.invalid",
                        Message = message,
                        Path = "rows"
                    })
                    .ToList()
            });
        }

        IReadOnlyList<JsonElement> documents;

        try
        {
            documents =
                await TabularDocuments.BuildAsync(
                    rows,
                    mapping,
                    cancellationToken);
        }
        catch (DataMappingValueException exception)
        {
            return (default, RenderResult.Failure(
                "rows.invalid_value",
                exception.Message,
                path: exception.SourceColumn,
                expected: exception.ExpectedType.ToString(),
                actual: exception.Value));
        }

        if (documents.Count == 0)
        {
            return (default, RenderResult.Failure(
                "rows.no_documents",
                "Las filas no han producido ningún documento.",
                path: "rows"));
        }

        if (documents.Count > 1)
        {
            var keys = string.Join(", ", mapping.Root.KeyColumns);

            return (default, RenderResult.Failure(
                "rows.multiple_documents",
                $"Las filas producen {documents.Count} documentos y cada petición emite uno. " +
                (keys.Length == 0
                    ? "Envía las filas de un solo documento."
                    : $"Envía las filas de un solo valor de {keys}."),
                path: "rows",
                expected: "1",
                actual: documents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return (documents[0], null);
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
