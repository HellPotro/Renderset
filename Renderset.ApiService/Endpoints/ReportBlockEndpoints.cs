using Renderset.Core.Blocks;
using Renderset.Core.Variables;

namespace Renderset.Api.Endpoints;

public static class ReportBlockEndpoints
{
    public static IEndpointRouteBuilder MapReportBlockEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/blocks")
                .WithTags("Blocks");

        group.MapGet(
            "/{tenantId}",
            GetAllAsync);

        group.MapGet(
            "/{tenantId}/type/{type}",
            GetByTypeAsync);

        group.MapGet(
            "/{tenantId}/{blockId}",
            GetByIdAsync);

        group.MapGet(
            "/{tenantId}/{blockId}/resolved",
            async (
                string tenantId,
                string blockId,
                string? culture,
                IReportBlockRepository blockRepository,
                IReportVariableResolver variableResolver,
                CancellationToken cancellationToken) =>
            {
                var block =
                    await blockRepository.GetByIdAsync(
                        tenantId,
                        blockId,
                        cancellationToken);

                if (block is null)
                    return Results.NotFound();

                var configurationJson =
                    await variableResolver.ResolveAsync(
                        tenantId,
                        block.ConfigurationJson,
                        culture,
                        cancellationToken);

                var resolved =
                    new ReportBlock
                    {
                        Id = block.Id,
                        Name = block.Name,
                        Type = block.Type,
                        ConfigurationJson = configurationJson
                    };

                return Results.Ok(resolved);
            });

        group.MapPut(
            "/{tenantId}/{blockId}",
            SaveAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string tenantId,
        IReportBlockRepository repository,
        CancellationToken cancellationToken)
    {
        var blocks =
            await repository.GetAllAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(blocks);
    }

    private static async Task<IResult> GetByTypeAsync(
        string tenantId,
        ReportBlockType type,
        IReportBlockRepository repository,
        CancellationToken cancellationToken)
    {
        var blocks =
            await repository.GetByTypeAsync(
                tenantId,
                type,
                cancellationToken);

        return Results.Ok(blocks);
    }

    private static async Task<IResult> GetByIdAsync(
        string tenantId,
        string blockId,
        IReportBlockRepository repository,
        CancellationToken cancellationToken)
    {
        var block =
            await repository.GetByIdAsync(
                tenantId,
                blockId,
                cancellationToken);

        return block is null
            ? Results.NotFound()
            : Results.Ok(block);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        string blockId,
        ReportBlock block,
        IReportBlockRepository repository,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                blockId,
                block.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "El blockId de la URL no coincide con ReportBlock.Id.");
        }

        await repository.SaveAsync(
            tenantId,
            block,
            cancellationToken);

        return Results.NoContent();
    }
}