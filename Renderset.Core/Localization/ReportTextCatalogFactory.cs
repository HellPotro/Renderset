namespace Renderset.Core.Localization;

public sealed class ReportTextCatalogFactory
    : IReportTextCatalogFactory
{
    private readonly IReportResourceRepository _resources;
    private readonly ITenantCultureRepository _cultures;

    public ReportTextCatalogFactory(
        IReportResourceRepository resources,
        ITenantCultureRepository cultures)
    {
        _resources = resources;
        _cultures = cultures;
    }

    public async Task<ReportTextCatalog> CreateAsync(
        string tenantId,
        string reportId,
        string? presetId,
        string? culture,
        CancellationToken cancellationToken = default)
    {
        var defaultCulture =
            await _cultures.GetDefaultAsync(
                tenantId,
                cancellationToken);

        var chain = BuildCultureChain(
            culture,
            defaultCulture?.Culture);

        if (chain.Count == 0)
            return ReportTextCatalog.Empty;

        var scopes = BuildScopes(
            reportId,
            presetId);

        var resources =
            await _resources.GetByScopesAsync(
                tenantId,
                scopes,
                cancellationToken);

        var entries = Flatten(
            resources,
            scopes,
            chain);

        return new ReportTextCatalog(
            entries,
            chain[0]);
    }

    /// <summary>
    /// Cadena de fallback de cultura: en-GB → en → cultura por defecto del
    /// tenant. Se aplica al construir el catálogo, no al consultarlo.
    /// </summary>
    internal static List<string> BuildCultureChain(
        string? culture,
        string? defaultCulture)
    {
        var chain = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (chain.Any(x =>
                    string.Equals(
                        x,
                        value,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            chain.Add(value);
        }

        Add(culture);

        if (!string.IsNullOrWhiteSpace(culture))
        {
            var separator = culture.IndexOf('-');

            if (separator > 0)
                Add(culture[..separator]);
        }

        Add(defaultCulture);

        if (!string.IsNullOrWhiteSpace(defaultCulture))
        {
            var separator = defaultCulture.IndexOf('-');

            if (separator > 0)
                Add(defaultCulture[..separator]);
        }

        return chain;
    }

    /// <summary>
    /// Ámbitos ordenados de menor a mayor precedencia: lo específico pisa a
    /// lo general.
    /// </summary>
    internal static List<string> BuildScopes(
        string reportId,
        string? presetId)
    {
        var scopes = new List<string>
        {
            ReportResourceScope.Global,
            ReportResourceScope.ForReport(reportId)
        };

        if (!string.IsNullOrWhiteSpace(presetId))
            scopes.Add(ReportResourceScope.ForPreset(presetId));

        return scopes;
    }

    internal static Dictionary<string, string> Flatten(
        IReadOnlyCollection<ReportResource> resources,
        IReadOnlyList<string> scopes,
        IReadOnlyList<string> cultureChain)
    {
        var entries = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        // Se recorren los ámbitos de menor a mayor precedencia y, dentro de
        // cada uno, las culturas de mayor a menor prioridad. Así el primer
        // valor escrito por ámbito es el de la mejor cultura disponible, y un
        // ámbito más específico siempre puede pisar al anterior.
        foreach (var scope in scopes)
        {
            var scopeResources = resources
                .Where(x =>
                    string.Equals(
                        x.Scope,
                        scope,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (scopeResources.Count == 0)
                continue;

            var resolvedInScope =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var culture in cultureChain)
            {
                foreach (var resource in scopeResources)
                {
                    if (!string.Equals(
                            resource.Culture,
                            culture,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(resource.Value))
                        continue;

                    resolvedInScope.TryAdd(
                        resource.Key,
                        resource.Value);
                }
            }

            foreach (var entry in resolvedInScope)
                entries[entry.Key] = entry.Value;
        }

        return entries;
    }
}
