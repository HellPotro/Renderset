using Renderset.Core.Definitions;

namespace Renderset.Core.Localization;

/// <summary>
/// Recorre una definición y produce las entradas de diccionario que le
/// corresponden. Se llama al crear un report desde la inferencia del JSON:
/// el diccionario de la cultura base queda completo sin que nadie escriba
/// nada, y el resto de culturas aparecen como pendientes.
/// </summary>
public static class ReportResourceSeeder
{
    public static IReadOnlyCollection<ReportResource> Build(
        ReportDefinition definition,
        string baseCulture,
        IReadOnlyCollection<string>? additionalCultures = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCulture);

        var scope = ReportResourceScope.ForReport(definition.Id);

        var seeds = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        void Add(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            seeds.TryAdd(key, value);
        }

        if (definition.Header is not null)
        {
            Add(ReportTextKeys.HeaderTitle, definition.Header.Title);
            Add(ReportTextKeys.HeaderSubtitle, definition.Header.Subtitle);

            foreach (var field in definition.Header.Fields)
                Add(ReportTextKeys.Field(field.Id), field.Label);
        }

        foreach (var section in definition.Sections)
        {
            Add(ReportTextKeys.Section(section.Id), section.Name);

            foreach (var field in section.Fields)
                Add(ReportTextKeys.Field(field.Id), field.Label);

            if (section.Table is null)
                continue;

            foreach (var column in section.Table.Columns)
            {
                Add(
                    ReportTextKeys.Column(section.Table.Id, column.Id),
                    column.Label);
            }
        }

        if (definition.Footer is not null)
            Add(ReportTextKeys.FooterText, definition.Footer.Text);

        var resources = new List<ReportResource>();

        foreach (var seed in seeds)
        {
            resources.Add(
                new ReportResource
                {
                    Scope = scope,
                    Key = seed.Key,
                    Culture = baseCulture,
                    Value = seed.Value,
                    Source = ReportResourceSource.Seed
                });

            foreach (var culture in additionalCultures ?? [])
            {
                if (string.Equals(
                        culture,
                        baseCulture,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resources.Add(
                    new ReportResource
                    {
                        Scope = scope,
                        Key = seed.Key,
                        Culture = culture,
                        Value = null,
                        Source = ReportResourceSource.Seed
                    });
            }
        }

        return resources;
    }
}
