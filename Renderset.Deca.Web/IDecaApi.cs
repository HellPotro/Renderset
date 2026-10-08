using Refit;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;
using Renderset.Core.Themes;

namespace Renderset.Deca.Web;

/// <summary>
/// La API de RenderSet que usa el portal: /api/deca y, para Marca, los temas,
/// la marca del tenant (logo y colores) y la subida del logo. Con la clave
/// de servicio del portal y el token del usuario (TenantTokenHandler).
/// </summary>
public interface IDecaApi
{
    /// <param name="from">yyyy-MM-dd</param>
    /// <param name="to">yyyy-MM-dd</param>
    [Get("/api/deca/{tenantId}")]
    Task<DecaPage> SearchAsync(
        string tenantId,
        [Query] string? search,
        [Query] string? from,
        [Query] string? to,
        [Query] int? skip,
        [Query] int? take,
        CancellationToken cancellationToken = default);

    [Get("/api/deca/{tenantId}/{decaId}")]
    Task<DecaDetail> GetAsync(
        string tenantId,
        string decaId,
        CancellationToken cancellationToken = default);

    [Post("/api/deca/{tenantId}")]
    Task<DecaDetail> IssueAsync(
        string tenantId,
        [Body] DecaData data,
        CancellationToken cancellationToken = default);

    [Get("/api/deca/{tenantId}/template")]
    Task<DecaTemplateResponse> GetTemplateAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/deca/{tenantId}/template")]
    Task<DecaTemplateResponse> SaveTemplateAsync(
        string tenantId,
        [Body] SaveDecaTemplateRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/deca/{tenantId}/template")]
    Task<DecaTemplateResponse> ResetTemplateAsync(
        string tenantId,
        [Query] int expectedVersion,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------ marca
    //
    // Viven en RenderSet: son del tenant, no del DeCA. Lo que se cambie aquí
    // se ve también en RenderSet (Temas, Compartir → Página pública).

    [Get("/api/themes/{tenantId}")]
    Task<IReadOnlyCollection<TenantReportTheme>> GetThemesAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Put("/api/themes/{tenantId}/{themeId}")]
    Task SaveThemeAsync(
        string tenantId,
        string themeId,
        [Body] TenantReportTheme theme,
        CancellationToken cancellationToken = default);

    [Delete("/api/themes/{tenantId}/{themeId}")]
    Task DeleteThemeAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logo, colores y nombre del tenant (los de la página pública).
    /// </summary>
    [Get("/api/sharing/{tenantId}/settings")]
    Task<DocumentSharingSettingsResponse> GetBrandAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rol Admin. Se manda la configuración entera: los textos de la página
    /// pública que aquí no se tocan van tal como se leyeron.
    /// </summary>
    [Put("/api/sharing/{tenantId}/settings")]
    Task SaveBrandAsync(
        string tenantId,
        [Body] DocumentSharingSettings settings,
        CancellationToken cancellationToken = default);

    [Multipart]
    [Post("/api/assets/{tenantId}/logo")]
    Task<TenantAssetResponse> UploadLogoAsync(
        string tenantId,
        [AliasAs("file")] StreamPart file,
        CancellationToken cancellationToken = default);

    [Post("/api/deca/{tenantId}/{decaId}/finish")]
    Task<DecaDetail> FinishAsync(
        string tenantId,
        string decaId,
        [Body] FinishDecaRequest request,
        CancellationToken cancellationToken = default);
}
