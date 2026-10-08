using Renderset.Core.Mapping;
using Renderset.Core.Reports;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Mappings de filas planas a JSON jerárquico.
///
///     GET    /api/mappings/{tenantId}?reportId=
///     GET    /api/mappings/{tenantId}/{mappingId}
///     PUT    /api/mappings/{tenantId}/{mappingId}           crea o actualiza; devuelve avisos frente al report
///     DELETE /api/mappings/{tenantId}/{mappingId}
///     POST   /api/mappings/{tenantId}/{mappingId}/preview   filas → JSON, sin emitir documento
///
/// Para emitir con filas: POST /api/render/{tenantId} con 'rows' (y
/// 'mappingId' opcional; sin él, el mapping asignado al report).
/// </summary>
public static class DataMappingEndpoints
{
    public static IEndpointRouteBuilder MapDataMappingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/mappings")
                .WithTags("Mappings");

        group.MapGet("/{tenantId}", GetAllAsync);
        group.MapGet("/{tenantId}/{mappingId}", GetAsync);
        group.MapPut("/{tenantId}/{mappingId}", SaveAsync);
        group.MapDelete("/{tenantId}/{mappingId}", DeleteAsync);
        group.MapPost("/{tenantId}/{mappingId}/preview", PreviewAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string tenantId,
        string? reportId,
        IDataMappingRepository mappings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            var forReport =
                await mappings.GetForReportAsync(
                    tenantId,
                    reportId,
                    cancellationToken);

            return Results.Ok(
                forReport is null
                    ? Array.Empty<DataMapping>()
                    : new[] { forReport });
        }

        return Results.Ok(
            await mappings.GetAllAsync(
                tenantId,
                cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        string mappingId,
        IDataMappingRepository mappings,
        CancellationToken cancellationToken)
    {
        var mapping =
            await mappings.GetByIdAsync(
                tenantId,
                mappingId,
                cancellationToken);

        return mapping is null
            ? Results.NotFound()
            : Results.Ok(mapping);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        string mappingId,
        DataMapping mapping,
        IDataMappingRepository mappings,
        IReportRepository reports,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(mapping.Id, mappingId, StringComparison.Ordinal))
        {
            return Results.BadRequest(
                new[] { "El identificador del mapping no coincide con el de la ruta." });
        }

        var errors =
            DataMappingRules.Validate(mapping).ToList();

        Report? report = null;

        if (!string.IsNullOrWhiteSpace(mapping.ReportId))
        {
            report =
                await reports.GetByIdAsync(
                    tenantId,
                    mapping.ReportId,
                    cancellationToken);

            if (report is null)
                errors.Add($"No existe el report '{mapping.ReportId}'.");
        }

        if (errors.Count > 0)
            return Results.BadRequest(errors);

        await mappings.SaveAsync(
            tenantId,
            mapping,
            cancellationToken);

        var saved =
            await mappings.GetByIdAsync(
                tenantId,
                mappingId,
                cancellationToken)
            ?? mapping;

        return Results.Ok(
            new DataMappingSaveResponse
            {
                Mapping = saved,
                Issues = report is null
                    ? []
                    : DataMappingRules.CompareWithReport(
                        saved,
                        report.DataSchema)
            });
    }

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        string mappingId,
        IDataMappingRepository mappings,
        CancellationToken cancellationToken)
    {
        var deleted =
            await mappings.DeleteAsync(
                tenantId,
                mappingId,
                cancellationToken);

        return deleted
            ? Results.NoContent()
            : Results.NotFound();
    }

    /// <summary>
    /// Para probar desde el ERP que las filas encajan, sin guardar ningún
    /// documento: devuelve los JSON que se usarían en el render.
    /// </summary>
    private static async Task<IResult> PreviewAsync(
        string tenantId,
        string mappingId,
        DataMappingPreviewRequest request,
        IDataMappingRepository mappings,
        CancellationToken cancellationToken)
    {
        var mapping =
            await mappings.GetByIdAsync(
                tenantId,
                mappingId,
                cancellationToken);

        if (mapping is null)
            return Results.NotFound();

        var errors =
            TabularDocuments.Validate(
                request.Rows,
                mapping);

        if (errors.Count > 0)
        {
            return Results.BadRequest(
                new DataMappingPreviewResponse { Errors = errors });
        }

        try
        {
            var documents =
                await TabularDocuments.BuildAsync(
                    request.Rows,
                    mapping,
                    cancellationToken);

            return Results.Ok(
                new DataMappingPreviewResponse
                {
                    DocumentCount = documents.Count,
                    Documents = documents
                        .Take(Math.Clamp(request.Take, 0, 20))
                        .ToList()
                });
        }
        catch (DataMappingValueException exception)
        {
            return Results.BadRequest(
                new DataMappingPreviewResponse { Errors = [exception.Message] });
        }
    }
}
