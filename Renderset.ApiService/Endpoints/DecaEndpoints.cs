using System.Net.Http.Headers;
using System.Security.Cryptography;
using Renderset.Api.Security;
using Renderset.Core.Rendering;
using Renderset.Core.Sharing;
using Renderset.Deca;
using Renderset.Deca.Issuing;
using Renderset.Deca.Validation;

namespace Renderset.Api.Endpoints;

/// <summary>
/// DeCA: emisión y consulta (privado, con clave y tenant) y la descarga del
/// QR (pública).
///
///     GET  /api/deca/{tenantId}                      listado
///     GET  /api/deca/{tenantId}/{decaId}             detalle
///     POST /api/deca/{tenantId}/validate             validar sin emitir
///     POST /api/deca/{tenantId}                      emitir
///     POST /api/deca/{tenantId}/{decaId}/finish      fin del servicio
///     GET  /api/deca/{tenantId}/{decaId}/pdf         el PDF (con clave), aunque el QR ya no descargue
///     GET    /api/deca/{tenantId}/template           diseño del tenant (o el base)
///     PUT    /api/deca/{tenantId}/template           guardar el diseño (versión nueva)
///     DELETE /api/deca/{tenantId}/template           volver al diseño base
///
///     GET  /q/{código}                               el PDF (QR)
/// </summary>
public static class DecaEndpoints
{
    private const string LoggerCategory = "Renderset.Deca";

    public static IEndpointRouteBuilder MapDecaEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deca")
            .WithTags("DeCA");

        group.MapGet("/{tenantId}", SearchAsync);
        group.MapGet("/{tenantId}/{decaId}", GetAsync);
        group.MapPost("/{tenantId}/validate", Validate);
        group.MapPost("/{tenantId}", IssueAsync);
        group.MapPost("/{tenantId}/{decaId}/finish", FinishAsync);
        group.MapGet("/{tenantId}/{decaId}/pdf", PdfAsync);

        // Ruta literal: tiene prioridad sobre /{tenantId}/{decaId}, y un id
        // de DeCA (32 hex) nunca se llama "template".
        group.MapGet("/{tenantId}/template", GetTemplateAsync);
        group.MapPut("/{tenantId}/template", SaveTemplateAsync);
        group.MapDelete("/{tenantId}/template", ResetTemplateAsync);

        return app;
    }

    /// <summary>
    /// La descarga del QR. Fuera de /api y sin autenticación: el inspector
    /// no tiene cuenta, y la norma no admite login, contraseña, CAPTCHA ni
    /// botones. La autorización es el código, que no se puede adivinar.
    ///
    /// Sólo lee: sirve el PDF guardado al emitir y nunca genera nada, así que
    /// no depende de Gotenberg ni de la plantilla, y lo que se descarga es
    /// byte a byte lo que se emitió.
    /// </summary>
    public static IEndpointRouteBuilder MapDecaPublicEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapMethods(
                DecaOptions.PublicPrefix + "/{code}",
                [HttpMethods.Get, HttpMethods.Head],
                DownloadAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SharingEndpoints.RateLimitPolicy)
            .ExcludeFromDescription();

        return app;
    }

    // ------------------------------------------------------------ privado

    private static async Task<IResult> SearchAsync(
        string tenantId,
        string? search,
        DateOnly? from,
        DateOnly? to,
        int? skip,
        int? take,
        IDecaRepository decas,
        CancellationToken cancellationToken)
    {
        var page =
            await decas.SearchAsync(
                tenantId,
                new DecaQuery
                {
                    Search = search,
                    From = from,
                    To = to,
                    Skip = skip ?? 0,
                    Take = take ?? 50
                },
                cancellationToken);

        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        string decaId,
        IDecaRepository decas,
        DecaOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var deca =
            await decas.GetAsync(
                tenantId,
                decaId,
                cancellationToken);

        return deca is null
            ? Results.NotFound()
            : Results.Ok(Detail(deca, options, time, []));
    }

    /// <summary>
    /// Las mismas reglas que al emitir, para el ERP que quiera comprobar
    /// antes. El portal valida en local con la misma librería.
    /// </summary>
    private static IResult Validate(
        string tenantId,
        DecaData data,
        DecaOptions options,
        TimeProvider time)
    {
        var result =
            DecaValidator.Validate(
                DecaNormalizer.Normalize(data ?? new DecaData()),
                time.GetUtcNow(),
                options.Zone);

        return Results.Ok(
            new DecaValidationResponse
            {
                IsValid = result.IsValid,
                Issues = result.Issues
            });
    }

    private static async Task<IResult> IssueAsync(
        string tenantId,
        DecaData data,
        HttpContext http,
        IDecaIssuer issuer,
        DecaOptions options,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var caller = http.User.GetCaller();

        var actor =
            caller?.UserId ??
            caller?.Name;

        var result =
            await issuer.IssueAsync(
                tenantId,
                data,
                actor,
                cancellationToken);

        if (result.Succeeded)
        {
            var detail =
                Detail(
                    result.Deca!,
                    options,
                    time,
                    result.Validation.Warnings.ToList());

            return Results.Created(
                $"/api/deca/{Uri.EscapeDataString(tenantId)}/{result.Deca!.Id}",
                detail);
        }

        // Datos: 400 con la lista de errores. Lo demás (PDF, dominio) es un
        // problema del servicio, no de quien llama.
        if (result.Failure is null)
        {
            return Results.Json(
                result.ToApiErrors(),
                statusCode: StatusCodes.Status400BadRequest);
        }

        loggers
            .CreateLogger(LoggerCategory)
            .LogError(
                "No se ha emitido un DeCA del tenant {TenantId}: {Code} {Message}",
                tenantId,
                result.Failure.Code,
                result.Failure.Message);

        return Results.Json(
            result.ToApiErrors(),
            statusCode: result.Failure.Code switch
            {
                "deca.pdf_too_large" => StatusCodes.Status422UnprocessableEntity,
                "deca.tenant_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status503ServiceUnavailable
            });
    }

    private static async Task<IResult> FinishAsync(
        string tenantId,
        string decaId,
        FinishDecaRequest? request,
        HttpContext http,
        IDecaRepository decas,
        DecaOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var deca =
            await decas.GetAsync(
                tenantId,
                decaId,
                cancellationToken);

        if (deca is null)
            return Results.NotFound();

        if (deca.Status != DecaStatus.Issued)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "deca.already_finished",
                "El servicio de este DeCA ya está terminado.",
                "decaId");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var ended = request?.EndedAtUtc is { } value
            ? value.UtcDateTime
            : now;

        // Ni antes de emitirlo ni en el futuro: el plazo de los siete días
        // cuenta desde algo que ya ha pasado.
        if (ended < deca.IssuedAtUtc || ended > now.AddMinutes(5))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "deca.finish_invalid_date",
                "La fecha de fin tiene que estar entre la emisión y ahora.",
                "endedAtUtc");
        }

        var caller = http.User.GetCaller();

        var finished =
            await decas.FinishServiceAsync(
                tenantId,
                decaId,
                ended,
                DecaAvailability.PublicUntil(ended, options),
                caller?.UserId ?? caller?.Name,
                cancellationToken);

        if (!finished)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "deca.already_finished",
                "El servicio de este DeCA ya está terminado.",
                "decaId");
        }

        var updated =
            await decas.GetAsync(
                tenantId,
                decaId,
                cancellationToken);

        return Results.Ok(Detail(updated!, options, time, []));
    }

    /// <summary>
    /// El PDF para el emisor: sin plazo (se conserva un año) y cualquier
    /// versión con ?version=N.
    /// </summary>
    private static async Task<IResult> PdfAsync(
        string tenantId,
        string decaId,
        int? version,
        bool? download,
        HttpContext http,
        IDecaRepository decas,
        CancellationToken cancellationToken)
    {
        var deca =
            await decas.GetAsync(
                tenantId,
                decaId,
                cancellationToken);

        if (deca is null)
            return Results.NotFound();

        var number = version ?? deca.CurrentVersion;

        var pdf =
            await decas.GetPdfAsync(
                tenantId,
                decaId,
                number,
                cancellationToken);

        if (pdf is null)
            return Results.NotFound();

        http.Response.Headers.CacheControl = "private, no-store";

        var fileName =
            number == deca.CurrentVersion
                ? $"{deca.Number}.pdf"
                : $"{deca.Number}-v{number}.pdf";

        return download == true
            ? Results.File(pdf, "application/pdf", fileName)
            : Inline(http, pdf, fileName);
    }

    private static IResult Inline(
        HttpContext http,
        byte[] pdf,
        string fileName)
    {
        http.Response.Headers.ContentDisposition =
            new ContentDispositionHeaderValue("inline") { FileNameStar = fileName }.ToString();

        return Results.Bytes(pdf, "application/pdf");
    }

    // -------------------------------------------------------------- diseño

    private static async Task<IResult> GetTemplateAsync(
        string tenantId,
        IDecaTemplateRepository templates,
        IDocumentSharingSettingsRepository sharing,
        CancellationToken cancellationToken)
    {
        var settings = await sharing.GetAsync(tenantId, cancellationToken);

        if (settings is null)
            return Results.NotFound();

        var template = await templates.GetCurrentAsync(tenantId, cancellationToken);

        return Results.Ok(
            TemplateResponse(
                template,
                settings.Normalized().ToBranding(tenantId)));
    }

    /// <summary>
    /// Guarda una versión nueva del diseño. Lo obligatorio que se haya
    /// ocultado se vuelve a mostrar (Enforce); si aun así faltase algo, 400
    /// con la lista. 409 si otro ha guardado entretanto.
    /// </summary>
    private static async Task<IResult> SaveTemplateAsync(
        string tenantId,
        SaveDecaTemplateRequest request,
        HttpContext http,
        IDecaTemplateRepository templates,
        IDocumentSharingSettingsRepository sharing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var settings = await sharing.GetAsync(tenantId, cancellationToken);

        if (settings is null)
            return Results.NotFound();

        var configuration = DecaTemplateGuard.Enforce(request.Configuration);
        var texts = request.Texts ?? [];

        var candidate =
            new DecaTemplate
            {
                TenantId = tenantId,
                Configuration = configuration,
                Theme = request.Theme,
                Texts = texts,
                UpdatedAtUtc = time.GetUtcNow().UtcDateTime,
                UpdatedBy = http.User.GetCaller() is { } caller
                    ? caller.UserId ?? caller.Name
                    : null
            };

        var resolved =
            new Renderset.Core.Services.ReportConfigurationResolver().Resolve(
                DecaReportTemplate.Definition,
                configuration,
                bodyBlocks: null,
                new Renderset.Core.Localization.ReportTextCatalog(
                    new Dictionary<string, string>(texts, StringComparer.OrdinalIgnoreCase),
                    "es-ES"));

        var missing = DecaTemplateGuard.Missing(resolved);

        if (missing.Count > 0)
        {
            return Results.Json(
                missing
                    .Select(x => new RenderValidationError
                    {
                        Code = "deca.template_missing",
                        Path = "configuration",
                        Message = $"El diseño tiene que mostrar: {x}."
                    })
                    .ToArray(),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await templates.AddVersionAsync(candidate, request.ExpectedVersion, cancellationToken))
        {
            return Error(
                StatusCodes.Status409Conflict,
                "deca.template_conflict",
                "Alguien ha guardado el diseño mientras lo editabas. Recarga la página para ver sus cambios.",
                "expectedVersion");
        }

        var saved = await templates.GetCurrentAsync(tenantId, cancellationToken);

        return Results.Ok(
            TemplateResponse(
                saved,
                settings.Normalized().ToBranding(tenantId)));
    }

    private static async Task<IResult> ResetTemplateAsync(
        string tenantId,
        int expectedVersion,
        HttpContext http,
        IDecaTemplateRepository templates,
        IDocumentSharingSettingsRepository sharing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var settings = await sharing.GetAsync(tenantId, cancellationToken);

        if (settings is null)
            return Results.NotFound();

        var current = await templates.GetCurrentAsync(tenantId, cancellationToken);

        if (current is null || current.IsBase)
            return Results.Ok(TemplateResponse(current, settings.Normalized().ToBranding(tenantId)));

        var reset =
            new DecaTemplate
            {
                TenantId = tenantId,
                Configuration = null,
                UpdatedAtUtc = time.GetUtcNow().UtcDateTime,
                UpdatedBy = http.User.GetCaller() is { } caller
                    ? caller.UserId ?? caller.Name
                    : null
            };

        if (!await templates.AddVersionAsync(reset, expectedVersion, cancellationToken))
        {
            return Error(
                StatusCodes.Status409Conflict,
                "deca.template_conflict",
                "Alguien ha guardado el diseño mientras lo editabas. Recarga la página para ver sus cambios.",
                "expectedVersion");
        }

        var saved = await templates.GetCurrentAsync(tenantId, cancellationToken);

        return Results.Ok(TemplateResponse(saved, settings.Normalized().ToBranding(tenantId)));
    }

    /// <summary>
    /// Siempre una configuración completa para el editor: la del tenant
    /// (corregida) o la base con su logo y colores.
    /// </summary>
    private static DecaTemplateResponse TemplateResponse(
        DecaTemplate? template,
        Renderset.Core.Tenancy.TenantBranding branding) =>
        template is { Configuration: { } configuration }
            ? new DecaTemplateResponse
            {
                Configuration = DecaTemplateGuard.Enforce(configuration),
                Theme = template.Theme ?? DecaReportTemplate.Theme(branding),
                Texts = template.Texts,
                Version = template.Version,
                IsBase = false,
                UpdatedAtUtc = template.UpdatedAtUtc,
                UpdatedBy = template.UpdatedBy
            }
            : new DecaTemplateResponse
            {
                Configuration = DecaReportTemplate.Configuration(branding),
                Theme = DecaReportTemplate.Theme(branding),
                Texts = [],
                Version = template?.Version ?? DecaTemplate.BaseVersion,
                IsBase = true,
                UpdatedAtUtc = template?.UpdatedAtUtc,
                UpdatedBy = template?.UpdatedBy
            };

    // ------------------------------------------------------------- público

    private static async Task<IResult> DownloadAsync(
        string code,
        bool? download,
        HttpContext http,
        IDecaRepository decas,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var headers = http.Response.Headers;

        // Ni buscadores ni cachés compartidas: el documento lleva NIF y
        // matrículas, y en una modificación cambia el PDF de la misma URL.
        headers["X-Robots-Tag"] = "noindex, nofollow";
        headers.CacheControl = "private, no-cache";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";

        var target =
            await decas.FindByPublicCodeAsync(
                code,
                cancellationToken);

        if (target is null)
        {
            return Results.Text(
                "No existe ningún DeCA con este código.",
                "text/plain; charset=utf-8",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (target.PublicUntilUtc is { } until &&
            time.GetUtcNow().UtcDateTime >= until)
        {
            return Results.Text(
                $"El DeCA {target.Number} ya no se puede descargar: el servicio terminó y ha pasado el plazo " +
                "de disponibilidad. El cargador y el transportista lo conservan.",
                "text/plain; charset=utf-8",
                statusCode: StatusCodes.Status410Gone);
        }

        var pdf =
            await decas.GetPdfAsync(
                target.TenantId,
                target.DecaId,
                target.CurrentVersion,
                cancellationToken);

        var logger = loggers.CreateLogger(LoggerCategory);

        if (pdf is null)
        {
            // No debería pasar: el PDF se guarda con (o antes que) el DeCA.
            logger.LogCritical(
                "El DeCA {Number} ({DecaId}) no tiene PDF guardado.",
                target.Number,
                target.DecaId);

            return Results.Text(
                "El documento no está disponible en este momento. Inténtalo de nuevo en unos minutos.",
                "text/plain; charset=utf-8",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Lo que se sirve tiene que ser lo que se emitió. Si no cuadra se
        // avisa con la máxima prioridad, pero se sirve: en una inspección
        // en carretera es peor no tener documento.
        var sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();

        if (!string.Equals(sha256, target.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogCritical(
                "El PDF del DeCA {Number} ({DecaId}) no coincide con su hash de emisión ({Expected} / {Actual}).",
                target.Number,
                target.DecaId,
                target.Sha256,
                sha256);
        }

        headers.ETag = $"\"{target.Sha256}\"";

        // inline: el móvil lo abre directamente, sin la pregunta de
        // "¿descargar?". ?download=true lo fuerza como fichero.
        var disposition =
            new ContentDispositionHeaderValue(download == true ? "attachment" : "inline")
            {
                FileNameStar = $"{target.Number}.pdf"
            };

        headers.ContentDisposition = disposition.ToString();

        return Results.Bytes(
            pdf,
            "application/pdf");
    }

    // ------------------------------------------------------------ comunes

    private static DecaDetail Detail(
        DecaDocument deca,
        DecaOptions options,
        TimeProvider time,
        List<DecaIssue> warnings) =>
        new()
        {
            Deca = deca,
            PublicUrl = options.PublicUrl(deca.PublicCode),
            IsPublicDownloadActive =
                DecaAvailability.IsPublicDownloadActive(
                    deca,
                    time.GetUtcNow().UtcDateTime),
            Warnings = warnings
        };

    private static IResult Error(
        int status,
        string code,
        string message,
        string path) =>
        Results.Json(
            new[]
            {
                new RenderValidationError
                {
                    Code = code,
                    Message = message,
                    Path = path
                }
            },
            statusCode: status);
}
