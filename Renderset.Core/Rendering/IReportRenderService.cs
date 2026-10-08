namespace Renderset.Core.Rendering;

public interface IReportRenderService
{
    Task<RenderResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo mismo que <see cref="RenderAsync"/> hasta antes de pintar, sin
    /// emitir ni guardar nada.
    /// </summary>
    Task<RenderValidationResponse> ValidateAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default);
}
