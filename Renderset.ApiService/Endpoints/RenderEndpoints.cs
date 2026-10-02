using Renderset.Core.Paging;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;

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
            "/{tenantId}",
            SearchDocumentsAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}",
            GetDocumentAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}/download",
            DownloadDocumentAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}/metadata",
            GetMetadataAsync);

        documents.MapGet(
            "/{tenantId}/{documentId}/pdf",
            GetPdfAsync);

        return app;
    }

    private static async Task<IResult> RenderAsync(
        string tenantId,
        RenderRequest request,
        HttpContext httpContext,
        IReportRenderService renderService,
        IDocumentPdfService pdfService,
        CancellationToken cancellationToken)
    {
        var wantsPdf =
            request.Output?.Format == RenderFormat.Pdf;

        // Se comprueba antes de emitir: no tiene sentido guardar un
        // documento para luego decir que el PDF no se puede hacer.
        if (wantsPdf && !pdfService.IsAvailable)
            return PdfNotConfigured();

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

        if (wantsPdf)
        {
            // Con formato Pdf se genera ya, para que quien integra pueda
            // descargarlo en cuanto recibe la respuesta. Si falla, el
            // documento HTML ya está emitido: el error lo dice para que se
            // pueda reintentar sólo el PDF.
            try
            {
                await pdfService.GetOrCreateAsync(
                    tenantId,
                    result.Document!.Id,
                    cancellationToken);
            }
            catch (PdfConversionException ex)
            {
                return PdfFailed(ex, result.Document!.Id);
            }
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
                PdfUrl = pdfService.IsAvailable
                    ? url + "/pdf"
                    : null,
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
    /// Listado de documentos emitidos, sin contenido, los más recientes
    /// primero y paginado por cursor:
    ///
    ///     GET /api/documents/{tenantId}?search=&amp;reportId=&amp;fromUtc=&amp;toUtc=&amp;take=&amp;cursor=
    ///
    /// Devuelve { items, nextCursor }. Para la página siguiente se repite la
    /// misma petición con cursor = nextCursor.
    /// </summary>
    private static async Task<IResult> SearchDocumentsAsync(
        string tenantId,
        string? search,
        string? reportId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        string? cursor,
        IRenderedDocumentRepository documents,
        CancellationToken cancellationToken)
    {
        KeysetCursor? decoded = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            decoded = KeysetCursor.TryDecode(cursor);

            if (decoded is null)
                return BundleEndpoints.InvalidCursor();
        }

        // DateTimeOffset y no DateTime: con "2026-10-01T22:00:00Z" o con
        // "+02:00" el instante es inequívoco, mientras que un DateTime sin
        // zona depende de cómo lo interprete el binder.
        var page =
            await documents.SearchAsync(
                tenantId,
                new RenderedDocumentQuery
                {
                    Search = search,
                    ReportId = reportId,
                    FromUtc = fromUtc?.UtcDateTime,
                    ToUtc = toUtc?.UtcDateTime,
                    Take = take ?? 50,
                    Cursor = decoded
                },
                cancellationToken);

        return Results.Ok(page);
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
        IDocumentPdfService pdfService,
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
                PdfUrl = pdfService.IsAvailable
                    ? BuildDocumentUrl(httpContext, tenantId, document.Id) + "/pdf"
                    : null,
                FileName = document.FileName,
                ReportId = document.ReportId,
                PresetId = document.PresetId,
                PresetVersion = document.PresetVersion,
                Culture = document.Culture,
                Format = document.Format,
                CreatedAtUtc = document.CreatedAtUtc
            });
    }

    /// <summary>
    /// PDF del documento. Se genera la primera vez que se pide y después se
    /// sirve el guardado.
    /// </summary>
    private static async Task<IResult> GetPdfAsync(
        string tenantId,
        string documentId,
        bool? download,
        IRenderedDocumentRepository documents,
        IDocumentPdfService pdfService,
        CancellationToken cancellationToken)
    {
        if (!pdfService.IsAvailable)
            return PdfNotConfigured();

        byte[]? pdf;

        try
        {
            pdf =
                await pdfService.GetOrCreateAsync(
                    tenantId,
                    documentId,
                    cancellationToken);
        }
        catch (PdfConversionException ex)
        {
            return PdfFailed(ex, documentId);
        }

        if (pdf is null)
            return Results.NotFound();

        var summary =
            (await documents.GetSummariesAsync(
                tenantId,
                [documentId],
                cancellationToken))
            .FirstOrDefault();

        var fileName =
            Path.GetFileNameWithoutExtension(summary?.FileName ?? documentId) + ".pdf";

        // Por defecto inline, para abrirlo en el navegador; ?download=true
        // lo fuerza como descarga.
        return download == true
            ? Results.File(pdf, "application/pdf", fileName)
            : Results.File(pdf, "application/pdf");
    }

    internal static IResult PdfNotConfigured() =>
        Results.Json(
            new[]
            {
                new RenderValidationError
                {
                    Code = "pdf.not_configured",
                    Message = "La API no tiene conversor de PDF configurado (Pdf:GotenbergUrl).",
                    Path = "output.format"
                }
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    internal static IResult PdfFailed(
        PdfConversionException ex,
        string documentId) =>
        Results.Json(
            new[]
            {
                new RenderValidationError
                {
                    Code = "pdf.generation_failed",
                    Message =
                        $"El documento '{documentId}' se ha emitido, pero no se ha " +
                        $"podido generar su PDF: {ex.Message}",
                    Path = "output.format",
                    Actual = documentId
                }
            },
            statusCode: StatusCodes.Status502BadGateway);

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
