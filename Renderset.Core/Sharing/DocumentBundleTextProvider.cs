using Renderset.Core.Localization;

namespace Renderset.Core.Sharing;

public interface IDocumentBundleTextProvider
{
    /// <summary>
    /// Textos de la página pública para una cultura: los del Diccionario del
    /// tenant y, donde falten, los incorporados.
    /// </summary>
    Task<DocumentBundleTexts> GetAsync(
        string tenantId,
        string? culture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea en el Diccionario las claves que falten para cada idioma del
    /// tenant. No pisa nada de lo que ya exista.
    /// </summary>
    Task SeedAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}


public sealed class DocumentBundleTextProvider
    : IDocumentBundleTextProvider
{
    private readonly IReportResourceRepository _resources;
    private readonly ITenantCultureRepository _cultures;

    public DocumentBundleTextProvider(
        IReportResourceRepository resources,
        ITenantCultureRepository cultures)
    {
        _resources = resources;
        _cultures = cultures;
    }

    public async Task<DocumentBundleTexts> GetAsync(
        string tenantId,
        string? culture,
        CancellationToken cancellationToken = default)
    {
        var resources =
            await _resources.GetByScopeAsync(
                tenantId,
                ReportResourceScope.Sharing,
                cancellationToken);

        return DocumentBundleTexts.For(
            culture,
            Overrides(resources, culture));
    }

    /// <summary>
    /// Sólo la cultura pedida y su idioma neutro (fr-BE → fr). A propósito
    /// no se cae a la cultura por defecto del tenant: para un cliente francés
    /// es mejor el francés incorporado que el texto español del tenant.
    /// </summary>
    internal static Dictionary<string, string> Overrides(
        IReadOnlyCollection<ReportResource> resources,
        string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture) || resources.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var chain =
            ReportTextCatalogFactory.BuildCultureChain(
                culture.Trim(),
                defaultCulture: null);

        return ReportTextCatalogFactory.Flatten(
            resources,
            [ReportResourceScope.Sharing],
            chain);
    }

    public async Task SeedAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var cultures =
            await _cultures.GetAllAsync(
                tenantId,
                cancellationToken);

        if (cultures.Count == 0)
            return;

        await _resources.SeedMissingAsync(
            tenantId,
            BuildSeed(cultures.Select(x => x.Culture)),
            cancellationToken);
    }

    /// <summary>
    /// En los idiomas con traducción incorporada se siembra ese texto, que es
    /// el que ya se estaba viendo. En el resto, la clave vacía: aparece como
    /// hueco en el Diccionario y se puede traducir a mano o con "Rellenar".
    /// </summary>
    internal static List<ReportResource> BuildSeed(
        IEnumerable<string> cultures)
    {
        var seed = new List<ReportResource>();

        foreach (var culture in cultures
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var builtIn =
                DocumentBundleTexts.HasBuiltInTexts(culture)
                    ? DocumentBundleTexts.For(culture)
                    : null;

            foreach (var (key, description) in DocumentBundleTextKeys.All)
            {
                seed.Add(new ReportResource
                {
                    Scope = ReportResourceScope.Sharing,
                    Key = key,
                    Culture = culture,
                    Value = builtIn?.Get(key),
                    Description = description,
                    Source = ReportResourceSource.Seed
                });
            }
        }

        return seed;
    }
}
