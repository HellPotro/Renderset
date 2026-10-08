using Refit;
using Renderset.Core.Blocks;
using Renderset.Core.Localization;
using Renderset.Core.Mapping;
using Renderset.Core.Paging;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Rendering;
using Renderset.Core.Reports;
using Renderset.Core.Resources;
using Renderset.Core.Security;
using Renderset.Core.Sharing;
using Renderset.Core.Themes;
using Renderset.Core.Translations;
using Renderset.Core.Variables;

namespace Renderset.Web;

public interface IRenderSetApi
{
    #region Cultures

    [Get("/api/cultures/{tenantId}")]
    Task<IReadOnlyCollection<TenantCulture>> GetCulturesAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/cultures/{tenantId}/{culture}")]
    Task<IApiResponse> SaveCultureAsync(
        string tenantId,
        string culture,
        [Body] TenantCulture body,
        CancellationToken cancellationToken = default);

    #endregion

    #region Resources

    [Get("/api/resources/{tenantId}/{scope}")]
    Task<IReadOnlyCollection<ReportResource>> GetResourcesAsync(
        string tenantId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Diccionario ya aplanado para una cultura: global + report (+ preset si
    /// se indica), con el fallback de cultura ya aplicado.
    /// </summary>
    [Get("/api/resources/{tenantId}/{scope}/catalog")]
    Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(
        string tenantId,
        string scope,
        [Query] string? presetId,
        [Query] string? culture,
        CancellationToken cancellationToken = default);

    [Put("/api/resources/{tenantId}")]
    Task<IApiResponse> SaveResourcesAsync(
        string tenantId,
        [Body] IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken = default);

    [Post("/api/resources/{tenantId}/translate-missing")]
    Task<IApiResponse<TranslateMissingReportResourcesResult>> TranslateMissingResourcesAsync(
        string tenantId,
        [Body] TranslateMissingReportResourcesRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/resources/{tenantId}/{scope}/{key}")]
    Task<IApiResponse> DeleteResourceAsync(
        string tenantId,
        string scope,
        string key,
        CancellationToken cancellationToken = default);

    [Get("/api/resources/{tenantId}/coverage")]
    Task<IReadOnlyCollection<ReportResourceCoverageDto>> GetResourceCoverageAsync(
        string tenantId,
        string? scope = null,
        CancellationToken cancellationToken = default);

    [Post("/api/resources/{tenantId}/copy-missing")]
    Task<IApiResponse<CopyMissingReportResourcesResult>> CopyMissingResourcesAsync(
        string tenantId,
        [Body] CopyMissingReportResourcesRequest request,
        CancellationToken cancellationToken = default);

    #endregion

    #region Assignments

    [Get("/api/assignments/{tenantId}/{reportId}")]
    Task<IReadOnlyCollection<ReportPresetAssignment>> GetAssignmentsAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Get("/api/assignments/{tenantId}/{reportId}/default")]
    Task<ReportPresetAssignment?> GetDefaultAssignmentAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Get("/api/assignments/{tenantId}/{reportId}/{contextType}/{contextKey}")]
    Task<ReportPresetAssignment?> GetAssignmentAsync(
        string tenantId,
        string reportId,
        string contextType,
        string contextKey,
        CancellationToken cancellationToken = default);

    [Put("/api/assignments/{tenantId}")]
    Task<IApiResponse> SaveAssignmentAsync(
        string tenantId,
        [Body] ReportPresetAssignment assignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alta en bloque (importación de CSV). Todas o ninguna.
    /// </summary>
    [Put("/api/assignments/{tenantId}/batch")]
    Task<IApiResponse> SaveAssignmentsAsync(
        string tenantId,
        [Body] IReadOnlyCollection<ReportPresetAssignment> assignments,
        CancellationToken cancellationToken = default);

    [Delete("/api/assignments/{tenantId}/id/{assignmentId}")]
    Task<IApiResponse> DeleteAssignmentAsync(
        string tenantId,
        long assignmentId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Blocks

    [Get("/api/blocks/{tenantId}")]
    Task<IReadOnlyCollection<ReportBlock>> GetBlocksAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/type/{type}")]
    Task<IReadOnlyCollection<ReportBlock>> GetBlocksByTypeAsync(
        string tenantId,
        ReportBlockType type,
        CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/{blockId}")]
    Task<ReportBlock> GetBlockAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/{blockId}/resolved")]
    Task<ReportBlock> GetResolvedBlockAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default);

    [Put("/api/blocks/{tenantId}/{blockId}")]
    Task<IApiResponse> SaveBlockAsync(
        string tenantId,
        string blockId,
        [Body] ReportBlock block,
        CancellationToken cancellationToken = default);

    #endregion

    #region Presets

    [Get("/api/presets/{tenantId}/{presetId}")]
    Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default);

    [Get("/api/presets/{tenantId}")]
    Task<IReadOnlyCollection<ReportPreset>> GetPresetsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Get("/api/presets/resolve/{tenantId}/{reportId}")]
    Task<ReportPreset?> ResolvePresetAsync(
        string tenantId,
        string reportId,
        [Query] string? contextType = null,
        [Query] string? contextKey = null,
        CancellationToken cancellationToken = default);

    [Put("/api/presets/{tenantId}/{presetId}")]
    Task<IApiResponse> SavePresetAsync(
        string tenantId,
        string presetId,
        [Body] ReportPreset preset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 409 si es el preset por defecto y el report tiene otros.
    /// </summary>
    [Delete("/api/presets/{tenantId}/{presetId}")]
    Task<IApiResponse> DeletePresetAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Render

    /// <summary>
    /// Siempre en Html desde la web: el PDF se genera al abrirlo por
    /// /files/documents/{id}/pdf, que tiene su propio cliente sin el
    /// timeout corto de la resiliencia estándar.
    /// </summary>
    [Post("/api/render/{tenantId}")]
    Task<IApiResponse<RenderDocumentResponse>> RenderAsync(
        string tenantId,
        [Body] RenderRequest request,
        CancellationToken cancellationToken = default);

    #endregion

    #region Mappings

    [Get("/api/mappings/{tenantId}")]
    Task<IReadOnlyCollection<DataMapping>> GetMappingsAsync(
        string tenantId,
        [Query] string? reportId = null,
        CancellationToken cancellationToken = default);

    [Put("/api/mappings/{tenantId}/{mappingId}")]
    Task<IApiResponse<DataMappingSaveResponse>> SaveMappingAsync(
        string tenantId,
        string mappingId,
        [Body] DataMapping mapping,
        CancellationToken cancellationToken = default);

    [Delete("/api/mappings/{tenantId}/{mappingId}")]
    Task<IApiResponse> DeleteMappingAsync(
        string tenantId,
        string mappingId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Themes

    [Get("/api/themes/{tenantId}")]
    Task<IReadOnlyCollection<TenantReportTheme>> GetThemesAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/themes/{tenantId}/{themeId}")]
    Task<IApiResponse> SaveThemeAsync(
        string tenantId,
        string themeId,
        [Body] TenantReportTheme theme,
        CancellationToken cancellationToken = default);

    [Delete("/api/themes/{tenantId}/{themeId}")]
    Task<IApiResponse> DeleteThemeAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Reports

    [Get("/api/reports/{tenantId}")]
    Task<IReadOnlyCollection<Report>> GetReportsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/{tenantId}/{reportId}")]
    Task<Report> GetReportAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Put("/api/reports/{tenantId}/{reportId}")]
    Task<IApiResponse> SaveReportAsync(
        string tenantId,
        string reportId,
        [Body] Report report,
        CancellationToken cancellationToken = default);

    [Delete("/api/reports/{tenantId}/{reportId}")]
    Task<IApiResponse> DeleteReportAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Variables

    [Get("/api/variables/{tenantId}")]
    Task<IReadOnlyCollection<ReportVariable>> GetVariablesAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/variables/{tenantId}/{key}")]
    Task<IApiResponse> SaveVariableAsync(
        string tenantId,
        string key,
        [Body] ReportVariable variable,
        CancellationToken cancellationToken = default);

    [Delete("/api/variables/{tenantId}/{key}")]
    Task<IApiResponse> DeleteVariableAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default);

    #endregion


    #region Documents

    /// <summary>
    /// Paginado por cursor: para la página siguiente se repite la llamada
    /// con cursor = NextCursor de la anterior. toUtc es exclusivo.
    /// </summary>
    [Get("/api/documents/{tenantId}")]
    Task<PagedResult<RenderedDocumentSummary>> SearchDocumentsAsync(
        string tenantId,
        [Query] string? search,
        [Query] string? reportId,
        [Query(Format = "o")] DateTimeOffset? fromUtc,
        [Query(Format = "o")] DateTimeOffset? toUtc,
        [Query] int? take,
        [Query] string? cursor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Datos del documento sin el contenido: nombre, report, idioma, fecha.
    /// </summary>
    [Get("/api/documents/{tenantId}/{documentId}/metadata")]
    Task<RenderDocumentResponse> GetDocumentMetadataAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Bundles

    /// <summary>
    /// Paginado por cursor, igual que los documentos. Cada bundle trae su
    /// Url si se puede recuperar.
    /// </summary>
    [Get("/api/bundles/{tenantId}")]
    Task<PagedResult<DocumentBundleResponse>> GetBundlesAsync(
        string tenantId,
        [Query] string? search,
        [Query] DocumentBundleStatus? status,
        [Query(Format = "o")] DateTimeOffset? fromUtc,
        [Query(Format = "o")] DateTimeOffset? toUtc,
        [Query] int? take,
        [Query] string? cursor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// La respuesta trae la URL del enlace. Es el único momento en que
    /// existe: después sólo se puede pedir una nueva.
    /// </summary>
    [Post("/api/bundles/{tenantId}")]
    Task<IApiResponse<DocumentBundleResponse>> CreateBundleAsync(
        string tenantId,
        [Body] CreateDocumentBundleRequest request,
        CancellationToken cancellationToken = default);

    [Post("/api/bundles/{tenantId}/{bundleId}/revoke")]
    Task<IApiResponse> RevokeBundleAsync(
        string tenantId,
        Guid bundleId,
        CancellationToken cancellationToken = default);

    [Post("/api/bundles/{tenantId}/{bundleId}/link")]
    Task<IApiResponse<DocumentBundleResponse>> RenewBundleLinkAsync(
        string tenantId,
        Guid bundleId,
        [Body] RenewDocumentBundleLinkRequest request,
        CancellationToken cancellationToken = default);

    #endregion

    #region Sharing settings

    [Get("/api/sharing/{tenantId}/settings")]
    Task<DocumentSharingSettingsResponse> GetSharingSettingsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/sharing/{tenantId}/settings")]
    Task<IApiResponse> SaveSharingSettingsAsync(
        string tenantId,
        [Body] DocumentSharingSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube el logo (PNG, JPG, WebP o GIF) y devuelve su URL pública. No
    /// cambia la configuración: la URL se guarda con el resto de la marca.
    /// </summary>
    [Multipart]
    [Post("/api/assets/{tenantId}/logo")]
    Task<Renderset.Core.Tenancy.TenantAssetResponse> UploadLogoAsync(
        string tenantId,
        [AliasAs("file")] StreamPart file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HTML de la página pública con la configuración sin guardar.
    /// </summary>
    [Post("/api/sharing/{tenantId}/preview")]
    Task<string> PreviewSharingAsync(
        string tenantId,
        [Body] DocumentSharingPreviewRequest request,
        CancellationToken cancellationToken = default);

    #endregion


    #region API keys

    [Get("/api/keys/{tenantId}")]
    Task<IReadOnlyList<ApiKeyResponse>> GetApiKeysAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// La respuesta trae la clave en claro: única vez que existe así.
    /// </summary>
    [Post("/api/keys/{tenantId}")]
    Task<IApiResponse<CreatedApiKeyResponse>> CreateApiKeyAsync(
        string tenantId,
        [Body] CreateApiKeyRequest request,
        CancellationToken cancellationToken = default);

    [Post("/api/keys/{tenantId}/{keyId}/revoke")]
    Task<IApiResponse> RevokeApiKeyAsync(
        string tenantId,
        Guid keyId,
        CancellationToken cancellationToken = default);

    #endregion
}
