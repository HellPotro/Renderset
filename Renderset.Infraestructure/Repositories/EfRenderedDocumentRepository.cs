using Microsoft.EntityFrameworkCore;
using Renderset.Core.Rendering;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfRenderedDocumentRepository
    : IRenderedDocumentRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfRenderedDocumentRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RenderedDocument?> GetByIdAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.RenderedDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.DocumentId == documentId,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task SaveAsync(
        string tenantId,
        RenderedDocument document,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.RenderedDocuments
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.DocumentId == document.Id,
                    cancellationToken);

        if (entity is null)
        {
            entity = new RenderedDocumentEntity
            {
                TenantId = tenantId,
                DocumentId = document.Id,
                CreatedAtUtc = document.CreatedAtUtc
            };

            context.RenderedDocuments.Add(entity);
        }

        entity.ReportId = document.ReportId;
        entity.PresetId = document.PresetId;
        entity.PresetVersion = document.PresetVersion;
        entity.Culture = document.Culture;
        entity.FileName = document.FileName;
        entity.Format = document.Format.ToString();
        entity.Content = document.Content;

        await context.SaveChangesAsync(cancellationToken);
    }

    private static RenderedDocument ToModel(
        RenderedDocumentEntity entity)
    {
        return new RenderedDocument
        {
            Id = entity.DocumentId,
            ReportId = entity.ReportId,
            PresetId = entity.PresetId,
            PresetVersion = entity.PresetVersion,
            Culture = entity.Culture,
            FileName = entity.FileName,
            Format = Enum.TryParse<RenderFormat>(
                entity.Format,
                ignoreCase: true,
                out var format)
                    ? format
                    : RenderFormat.Html,
            Content = entity.Content,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
