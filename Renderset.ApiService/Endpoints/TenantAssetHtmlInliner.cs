using Renderset.Core.Rendering;
using Renderset.Core.Tenancy;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Incrusta los logos subidos a RenderSet (/assets/...) leyéndolos de su
/// almacén, sin pedirlos por HTTP a la propia API, y deja el resto para el
/// inliner normal (HttpHtmlAssetInliner).
///
/// Pedirlos por HTTP fallaría en local (la API es localhost y el inliner no
/// descarga de redes privadas) y, en una emisión en lote, gastaría el límite
/// de peticiones de las rutas públicas.
/// </summary>
public sealed class TenantAssetHtmlInliner(
    IHtmlAssetInliner inner,
    IServiceScopeFactory scopes,
    TenantAssetOptions options,
    DocumentAssetsOptions documentAssets)
    : IHtmlAssetInliner
{
    public async Task<string> InlineAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (documentAssets.InlineImages &&
            !string.IsNullOrEmpty(html) &&
            options.BaseUrl() is { } baseUrl)
        {
            var prefix = baseUrl + TenantAssetEndpoints.PublicPrefix + "/";

            var own =
                HtmlImageSources
                    .FindRemote(html)
                    .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (own.Count > 0)
                html = HtmlImageSources.Replace(html, await ReadAsync(own, prefix, cancellationToken));
        }

        return await inner.InlineAsync(html, cancellationToken);
    }

    private async Task<Dictionary<string, string>> ReadAsync(
        IReadOnlyList<string> urls,
        string prefix,
        CancellationToken cancellationToken)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

        // El inliner es único para toda la API y el repositorio va por
        // petición: se abre un ámbito sólo para esto.
        await using var scope = scopes.CreateAsyncScope();

        var assets = scope.ServiceProvider.GetRequiredService<ITenantAssetRepository>();

        foreach (var url in urls)
        {
            // {tenant}/{id}.{ext}, sin consulta ni fragmento.
            var path = url[prefix.Length..].Split('?', '#')[0];
            var parts = path.Split('/');

            if (parts.Length != 2)
                continue;

            var assetId = TenantAssetRules.ParsePublicFileName(parts[1]);

            if (assetId is null)
                continue;

            var found =
                await assets.GetAsync(
                    Uri.UnescapeDataString(parts[0]),
                    assetId,
                    cancellationToken);

            if (found is { } asset)
                replacements[url] = $"data:{asset.Asset.ContentType};base64,{Convert.ToBase64String(asset.Content)}";
        }

        return replacements;
    }
}
