using Refit;

namespace Renderset.Deca.Web;

/// <summary>
/// /api/deca de la API. Lo registra el host (Renderset.Web) con el mismo
/// HttpClient que IRenderSetApi: clave de servicio y token del usuario.
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

    [Post("/api/deca/{tenantId}/{decaId}/finish")]
    Task<DecaDetail> FinishAsync(
        string tenantId,
        string decaId,
        [Body] FinishDecaRequest request,
        CancellationToken cancellationToken = default);
}
