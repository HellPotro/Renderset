namespace Renderset.Core.Rendering;

public interface IReportRenderService
{
    Task<RenderResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        CancellationToken cancellationToken = default);
}
