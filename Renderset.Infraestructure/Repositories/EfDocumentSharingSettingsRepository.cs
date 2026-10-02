using Microsoft.EntityFrameworkCore;
using Renderset.Core.Sharing;
using Renderset.Infrastructure.Persistence;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// Configuración de la página pública, guardada en la fila del tenant: son
/// pocos valores, uno por tenant, y el logo y los colores son la marca del
/// tenant, no algo exclusivo de esta página.
/// </summary>
public sealed class EfDocumentSharingSettingsRepository
    : IDocumentSharingSettingsRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfDocumentSharingSettingsRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DocumentSharingSettings?> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.Tenants
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new DocumentSharingSettings
            {
                TenantName = x.Name,
                LogoUrl = x.LogoUrl,
                PrimaryColor = x.PrimaryColor,
                SecondaryColor = x.SecondaryColor,
                DefaultMessage = x.SharingDefaultMessage,
                FooterText = x.SharingFooterText,
                DefaultExpirationDays = x.SharingDefaultExpirationDays
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(
        string tenantId,
        DocumentSharingSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var value = settings.Normalized();

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var tenant =
            await context.Tenants
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId,
                    cancellationToken);

        if (tenant is null)
            return false;

        // TenantName no se toca: renombrar el tenant no es cosa de esta
        // pantalla.
        tenant.LogoUrl = value.LogoUrl;
        tenant.PrimaryColor = value.PrimaryColor;
        tenant.SecondaryColor = value.SecondaryColor;
        tenant.SharingDefaultMessage = value.DefaultMessage;
        tenant.SharingFooterText = value.FooterText;
        tenant.SharingDefaultExpirationDays = value.DefaultExpirationDays;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
