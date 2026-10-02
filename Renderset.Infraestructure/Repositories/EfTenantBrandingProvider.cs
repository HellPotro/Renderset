using Microsoft.EntityFrameworkCore;
using Renderset.Core.Tenancy;
using Renderset.Infrastructure.Persistence;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// Marca del tenant leída de la tabla Tenants.
/// </summary>
public sealed class EfTenantBrandingProvider
    : ITenantBrandingProvider
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfTenantBrandingProvider(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<TenantBranding> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var tenant =
            await context.Tenants
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .Select(x => new
                {
                    x.Name,
                    x.LogoUrl,
                    x.PrimaryColor,
                    x.SecondaryColor
                })
                .FirstOrDefaultAsync(cancellationToken);

        return TenantBranding.Create(
            tenantId,
            tenant?.Name,
            tenant?.LogoUrl,
            tenant?.PrimaryColor,
            tenant?.SecondaryColor);
    }
}
