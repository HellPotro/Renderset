using Renderset.Core.Variables;

public static class ReportVariableEndpoints
{
    public static IEndpointRouteBuilder MapReportVariableEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/variables")
                .WithTags("Variables");

        group.MapGet(
            "/{tenantId}",
            async (
                string tenantId,
                IReportVariableRepository repository,
                CancellationToken cancellationToken) =>
            {
                var variables =
                    await repository.GetAllAsync(
                        tenantId,
                        cancellationToken);

                return Results.Ok(variables);
            });

        group.MapPut(
            "/{tenantId}/{key}",
            async (
                string tenantId,
                string key,
                ReportVariable variable,
                IReportVariableRepository repository,
                CancellationToken cancellationToken) =>
            {
                if (!string.Equals(
                        key,
                        variable.Key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest();
                }

                await repository.SaveAsync(
                    tenantId,
                    variable,
                    cancellationToken);

                return Results.NoContent();
            });

        group.MapDelete(
            "/{tenantId}/{key}",
            async (
                string tenantId,
                string key,
                IReportVariableRepository repository,
                CancellationToken cancellationToken) =>
            {
                await repository.DeleteAsync(
                    tenantId,
                    key,
                    cancellationToken);

                return Results.NoContent();
            });

        return app;
    }
}