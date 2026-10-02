using System.Text;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Paging;
using Renderset.Core.Rendering;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// Documentos emitidos.
///
/// El contenido va a <see cref="IDocumentContentStore"/> si hay uno
/// registrado y a la columna Content si no. Al leer se mira la fila: si trae
/// ruta, se va al almacén; si no, el contenido está en la propia fila. Así
/// los documentos emitidos antes de activar el blob se siguen abriendo sin
/// migrar nada.
///
/// El PDF sigue la misma regla (PdfPath en el almacén o PdfContent en la
/// fila) y nunca se carga salvo que se pida: es la columna más pesada.
/// </summary>
public sealed class EfRenderedDocumentRepository
    : IRenderedDocumentRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;
    private readonly IDocumentContentStore? _contentStore;

    /// <param name="contentStore">
    /// Opcional. El contenedor de dependencias pasa null cuando no hay
    /// ningún almacén registrado, que es la configuración por defecto.
    /// </param>
    public EfRenderedDocumentRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory,
        IDocumentContentStore? contentStore = null)
    {
        _contextFactory = contextFactory;
        _contentStore = contentStore;
    }

    public async Task<RenderedDocument?> GetByIdAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        // Proyección explícita para no traer PdfContent.
        var row =
            await context.RenderedDocuments
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.DocumentId == documentId)
                .Select(x => new
                {
                    Entity = new RenderedDocumentEntity
                    {
                        TenantId = x.TenantId,
                        DocumentId = x.DocumentId,
                        ReportId = x.ReportId,
                        PresetId = x.PresetId,
                        PresetVersion = x.PresetVersion,
                        Culture = x.Culture,
                        FileName = x.FileName,
                        Format = x.Format,
                        Content = x.Content,
                        ContentPath = x.ContentPath,
                        CreatedAtUtc = x.CreatedAtUtc
                    },
                    HasPdf = x.PdfPath != null || x.PdfContent != null
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var content =
            await ReadContentAsync(
                row.Entity,
                cancellationToken);

        return ToModel(row.Entity, content, row.HasPdf);
    }

    public async Task<IReadOnlyList<RenderedDocumentSummary>> GetSummariesAsync(
        string tenantId,
        IReadOnlyCollection<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
            return [];

        var ids =
            documentIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        // Se proyecta sin Content a propósito: es la columna pesada y aquí
        // no hace falta.
        var rows =
            await context.RenderedDocuments
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    ids.Contains(x.DocumentId))
                .Select(x => new
                {
                    x.DocumentId,
                    x.ReportId,
                    x.Culture,
                    x.FileName,
                    x.Format,
                    x.CreatedAtUtc,
                    HasPdf = x.PdfPath != null || x.PdfContent != null
                })
                .ToListAsync(cancellationToken);

        return rows
            .Select(x => new RenderedDocumentSummary
            {
                Id = x.DocumentId,
                ReportId = x.ReportId,
                Culture = x.Culture,
                FileName = x.FileName,
                Format = ParseFormat(x.Format),
                HasPdf = x.HasPdf,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();
    }

    public async Task<PagedResult<RenderedDocumentSummary>> SearchAsync(
        string tenantId,
        RenderedDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var documents =
            context.RenderedDocuments
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.ReportId))
        {
            var reportId = query.ReportId.Trim();

            documents = documents.Where(x => x.ReportId == reportId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            documents = documents.Where(x =>
                x.FileName.Contains(search) ||
                x.DocumentId.Contains(search));
        }

        if (query.FromUtc is { } fromUtc)
            documents = documents.Where(x => x.CreatedAtUtc >= fromUtc);

        if (query.ToUtc is { } toUtc)
            documents = documents.Where(x => x.CreatedAtUtc < toUtc);

        if (query.Cursor is { } cursor)
        {
            var at = cursor.CreatedAtUtc;
            var seen = cursor.SeenIds.ToList();

            documents = documents.Where(x =>
                x.CreatedAtUtc < at ||
                (x.CreatedAtUtc == at && !seen.Contains(x.DocumentId)));
        }

        var take = Math.Clamp(query.Take, 1, RenderedDocumentQuery.MaxTake);

        // Una fila de más para saber si hay otra página sin hacer un COUNT.
        var rows =
            await documents
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.DocumentId)
                .Take(take + 1)
                .Select(x => new
                {
                    x.DocumentId,
                    x.ReportId,
                    x.Culture,
                    x.FileName,
                    x.Format,
                    x.CreatedAtUtc,
                    HasPdf = x.PdfPath != null || x.PdfContent != null
                })
                .ToListAsync(cancellationToken);

        var summaries =
            rows
                .Select(x => new RenderedDocumentSummary
                {
                    Id = x.DocumentId,
                    ReportId = x.ReportId,
                    Culture = x.Culture,
                    FileName = x.FileName,
                    Format = ParseFormat(x.Format),
                    HasPdf = x.HasPdf,
                    CreatedAtUtc = DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc)
                })
                .ToList();

        return KeysetCursor.Page(
            summaries,
            take,
            x => x.CreatedAtUtc,
            x => x.Id);
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

        if (_contentStore is null)
        {
            entity.Content = document.Content;
            entity.ContentPath = null;
        }
        else
        {
            // Primero el blob y después la fila: si falla el blob no queda
            // una fila apuntando a algo que no existe. Al revés (blob sin
            // fila) sólo queda un fichero huérfano, que no rompe nada.
            entity.ContentPath =
                await _contentStore.SaveAsync(
                    tenantId,
                    document.Id,
                    document.FileName,
                    ContentType(document.Format),
                    Encoding.UTF8.GetBytes(document.Content),
                    cancellationToken);

            entity.Content = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]?> GetPdfAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var row =
            await context.RenderedDocuments
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.DocumentId == documentId)
                .Select(x => new
                {
                    x.PdfPath,
                    x.PdfContent
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        if (row.PdfContent is not null)
            return row.PdfContent;

        if (string.IsNullOrWhiteSpace(row.PdfPath) || _contentStore is null)
            return null;

        // Si el fichero ha desaparecido del almacén se devuelve nulo y el
        // servicio lo vuelve a generar desde el HTML: el PDF es derivable.
        return await _contentStore.ReadAsync(
            row.PdfPath,
            cancellationToken);
    }

    public async Task SavePdfAsync(
        string tenantId,
        string documentId,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var fileName =
            await context.RenderedDocuments
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.DocumentId == documentId)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken);

        if (fileName is null)
            return;

        string? path = null;
        byte[]? inline = pdf;

        if (_contentStore is not null)
        {
            path =
                await _contentStore.SaveAsync(
                    tenantId,
                    documentId,
                    PdfFileName(fileName),
                    "application/pdf",
                    pdf,
                    cancellationToken);

            inline = null;
        }

        var now = DateTime.UtcNow;

        await context.RenderedDocuments
            .Where(x =>
                x.TenantId == tenantId &&
                x.DocumentId == documentId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.PdfPath, path)
                    .SetProperty(x => x.PdfContent, inline)
                    .SetProperty(x => x.PdfCreatedAtUtc, (DateTime?)now),
                cancellationToken);
    }

    /// <summary>
    /// "factura-0412.html" → "factura-0412.pdf".
    /// </summary>
    public static string PdfFileName(
        string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);

        return (string.IsNullOrWhiteSpace(name) ? "documento" : name) + ".pdf";
    }

    private async Task<string> ReadContentAsync(
        RenderedDocumentEntity entity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.ContentPath))
            return entity.Content ?? string.Empty;

        if (_contentStore is null)
        {
            throw new InvalidOperationException(
                $"El documento '{entity.DocumentId}' tiene el contenido en un " +
                $"almacén externo ('{entity.ContentPath}'), pero la API no tiene " +
                "ninguno configurado. Revisa la sección DocumentStorage.");
        }

        var bytes =
            await _contentStore.ReadAsync(
                entity.ContentPath,
                cancellationToken);

        if (bytes is null)
        {
            throw new InvalidOperationException(
                $"No se encuentra el contenido del documento '{entity.DocumentId}' " +
                $"en '{entity.ContentPath}'.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string ContentType(
        RenderFormat format) =>
        format == RenderFormat.Pdf
            ? "application/pdf"
            : "text/html; charset=utf-8";

    private static RenderFormat ParseFormat(
        string? value) =>
        Enum.TryParse<RenderFormat>(
            value,
            ignoreCase: true,
            out var format)
            ? format
            : RenderFormat.Html;

    private static RenderedDocument ToModel(
        RenderedDocumentEntity entity,
        string content,
        bool hasPdf)
    {
        return new RenderedDocument
        {
            Id = entity.DocumentId,
            ReportId = entity.ReportId,
            PresetId = entity.PresetId,
            PresetVersion = entity.PresetVersion,
            Culture = entity.Culture,
            FileName = entity.FileName,
            Format = ParseFormat(entity.Format),
            Content = content,
            HasPdf = hasPdf,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
