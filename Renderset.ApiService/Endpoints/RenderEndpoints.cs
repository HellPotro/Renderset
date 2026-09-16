using Renderset.Core.Rendering;

namespace Renderset.Api.Endpoints;

public static class RenderEndpoints
{
    public static IEndpointRouteBuilder MapRenderEndpoints(
        this IEndpointRouteBuilder app)
    {
        var render =
            app.MapGroup("/api/render")
                .WithTags("Render");

        render.MapPost(
            "/{tenantId}",
            RenderAsync);

        var documents =
            app.MapGroup("/api/documents")
                .WithTags("Documents");

        documents.MapGet(
            "/{tenantId}/{documentId}",
            GetDocumentAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}/download",
            DownloadDocumentAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}/metadata",
            GetMetadataAsync);

        return app;
    }

    private static async Task<IResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        HttpContext httpContext,
        IReportRenderService renderService,
        CancellationToken cancellationToken)
    {
        var result =
            await renderService.RenderAsync(
                tenantId,
                request,
                cancellationToken);

        if (!result.Succeeded)
        {
            // Los errores son de validación y llevan código y path: quien
            // integra tiene que poder saber qué corregir sin abrir el log.
            return Results.BadRequest(result.Errors);
        }

        var document = result.Document!;

        var url =
            BuildDocumentUrl(
                httpContext,
                tenantId,
                document.Id);

        var response =
            new RenderDocumentResponse
            {
                DocumentId = document.Id,
                Url = url,
                FileName = document.FileName,
                ReportId = document.ReportId,
                PresetId = document.PresetId,
                PresetVersion = document.PresetVersion,
                Culture = document.Culture,
                Format = document.Format,
                CreatedAtUtc = document.CreatedAtUtc
            };

        return Results.Created(url, response);
    }

    /// <summary>
    /// Devuelve el documento para verlo en el navegador. Es el link que
    /// entrega /api/render.
    /// </summary>
    private static async Task<IResult> GetDocumentAsync(
        string tenantId,
        string documentId,
        IRenderedDocumentRepository documents,
        CancellationToken cancellationToken)
    {
        var document =
            await documents.GetByIdAsync(
                tenantId,
                documentId,
                cancellationToken);

        if (document is null)
            return Results.NotFound();

        return Results.Content(
            document.Content,
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> DownloadDocumentAsync(
        string tenantId,
        string documentId,
        IRenderedDocumentRepository documents,
        CancellationToken cancellationToken)
    {
        var document =
            await documents.GetByIdAsync(
                tenantId,
                documentId,
                cancellationToken);

        if (document is null)
            return Results.NotFound();

        return Results.File(
            System.Text.Encoding.UTF8.GetBytes(document.Content),
            "text/html; charset=utf-8",
            document.FileName);
    }

    private static async Task<IResult> GetMetadataAsync(
        string tenantId,
        string documentId,
        HttpContext httpContext,
        IRenderedDocumentRepository documents,
        CancellationToken cancellationToken)
    {
        var document =
            await documents.GetByIdAsync(
                tenantId,
                documentId,
                cancellationToken);

        if (document is null)
            return Results.NotFound();

        return Results.Ok(
            new RenderDocumentResponse
            {
                DocumentId = document.Id,
                Url = BuildDocumentUrl(
                    httpContext,
                    tenantId,
                    document.Id),
                FileName = document.FileName,
                ReportId = document.ReportId,
                PresetId = document.PresetId,
                PresetVersion = document.PresetVersion,
                Culture = document.Culture,
                Format = document.Format,
                CreatedAtUtc = document.CreatedAtUtc
            });
    }

    private static string BuildDocumentUrl(
        HttpContext httpContext,
        string tenantId,
        string documentId)
    {
        var request = httpContext.Request;

        return
            $"{request.Scheme}://{request.Host}" +
            $"/api/documents/{Uri.EscapeDataString(tenantId)}" +
            $"/{Uri.EscapeDataString(documentId)}";
    }
}
