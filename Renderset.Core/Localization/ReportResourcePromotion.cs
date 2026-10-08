using Renderset.Core.Configurations;
using Renderset.Core.Definitions;

namespace Renderset.Core.Localization;

/// <summary>
/// Pasa al ámbito del report los textos que están repetidos en sus presets.
///
/// El diseñador escribe siempre en el ámbito del preset, y al duplicar un
/// preset se copiaban sus textos. El resultado era la misma clave con el
/// mismo valor en cada preset ("Destino" en preset:a y en preset:b), que hay
/// que traducir una vez por preset. Como el diccionario se lee por capas
/// (común → report → preset), una sola fila en el report vale para todos.
///
/// Sólo se mueve lo que no cambia nada para nadie:
///
///   - Los presets que tienen la clave tienen que coincidir en el valor de
///     cada idioma. Si uno dice "Destino" y otro "Entrega", es un override
///     de verdad y se queda donde está.
///   - Un preset que no tiene la clave pero la usa (cualquier preset para
///     las claves de la definición; para una sección personalizada, sólo
///     los que tienen esa sección) heredaría el valor nuevo del report. Si
///     eso le cambia el texto, la clave no se mueve.
///
/// Un preset que tenía la clave en un idioma y no en otro gana la
/// traducción que tenía su hermano: es justo lo que se busca.
///
/// Es una función pura: quien la usa (duplicar preset, diccionario) decide
/// cómo aplicar el plan.
/// </summary>
public static class ReportResourcePromotion
{
    public sealed record PresetInfo(
        string PresetId,
        string Name,
        ReportConfiguration Configuration)
    {
        public string Scope =>
            ReportResourceScope.ForPreset(PresetId);
    }

    public sealed record Skipped(
        string Key,
        string Reason);

    public sealed record Plan(
        IReadOnlyList<ReportResource> ReportWrites,
        IReadOnlyList<(string Scope, string Key)> Deletions,
        IReadOnlyList<string> PromotedKeys,
        IReadOnlyList<Skipped> SkippedKeys)
    {
        public bool IsEmpty =>
            PromotedKeys.Count == 0;
    }

    /// <param name="presetResources">Textos de cada preset del report, por id de preset.</param>
    /// <param name="minHolders">
    /// Cuántos presets tienen que tener la clave para moverla. 2 en el
    /// diccionario (sólo lo repetido); al duplicar, el nuevo cuenta como uno.
    /// </param>
    /// <param name="onlyKeys">Limita el plan a estas claves.</param>
    public static Plan Build(
        ReportDefinition definition,
        IReadOnlyList<PresetInfo> presets,
        IReadOnlyCollection<ReportResource> reportResources,
        IReadOnlyDictionary<string, IReadOnlyCollection<ReportResource>> presetResources,
        int minHolders = 2,
        IReadOnlyCollection<string>? onlyKeys = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var reportScope = ReportResourceScope.ForReport(definition.Id);

        var definitionSections = DefinitionSectionIds(definition);

        var writes = new List<ReportResource>();
        var deletions = new List<(string Scope, string Key)>();
        var promoted = new List<string>();
        var skipped = new List<Skipped>();

        // Valor no vacío de cada preset, por clave y cultura.
        var byPreset =
            presets.ToDictionary(
                x => x.PresetId,
                x => (presetResources.TryGetValue(x.PresetId, out var list) ? list : Array.Empty<ReportResource>())
                    .Where(r => !string.IsNullOrWhiteSpace(r.Value))
                    .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(r => r.Culture, StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(c => c.Key, c => c.First(), StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var report =
            reportResources
                .Where(r => !string.IsNullOrWhiteSpace(r.Value))
                .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(r => r.Culture, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(c => c.Key, c => c.First().Value!, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

        var keys =
            byPreset.Values
                .SelectMany(x => x.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(key => onlyKeys is null || onlyKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var key in keys)
        {
            var holders =
                presets
                    .Where(p => byPreset[p.PresetId].ContainsKey(key))
                    .ToList();

            if (holders.Count < minHolders)
                continue;

            // Un valor por cultura, el mismo en todos los que lo tienen.
            var values = new Dictionary<string, ReportResource>(StringComparer.OrdinalIgnoreCase);
            string? conflict = null;

            foreach (var holder in holders)
            {
                foreach (var (culture, resource) in byPreset[holder.PresetId][key])
                {
                    if (!values.TryGetValue(culture, out var existing))
                    {
                        values[culture] = resource;
                        continue;
                    }

                    if (!string.Equals(existing.Value!.Trim(), resource.Value!.Trim(), StringComparison.Ordinal))
                    {
                        conflict = $"En {culture} los presets no dicen lo mismo";
                        break;
                    }
                }

                if (conflict is not null)
                    break;
            }

            if (conflict is not null)
            {
                skipped.Add(new Skipped(key, conflict));
                continue;
            }

            report.TryGetValue(key, out var current);

            bool Changes(string culture) =>
                current is null ||
                !current.TryGetValue(culture, out var now) ||
                !string.Equals(now.Trim(), values[culture].Value!.Trim(), StringComparison.Ordinal);

            // Presets que usan la clave pero no la tienen: heredarían el
            // valor nuevo del report.
            var affected =
                presets
                    .Where(p => !holders.Contains(p))
                    .Where(p => UsesKey(p, key, definitionSections))
                    .FirstOrDefault(_ => values.Keys.Any(Changes));

            if (affected is not null)
            {
                skipped.Add(new Skipped(key, $"Cambiaría el texto del preset {affected.Name}"));
                continue;
            }

            foreach (var (culture, resource) in values)
            {
                if (!Changes(culture))
                    continue;

                writes.Add(
                    new ReportResource
                    {
                        Scope = reportScope,
                        Key = key,
                        Culture = culture,
                        Value = resource.Value!.Trim(),
                        Source = resource.Source,
                        Description = resource.Description,
                        NeedsReview = resource.NeedsReview,
                        TranslationProvider = resource.TranslationProvider,
                        MachineTranslatedAtUtc = resource.MachineTranslatedAtUtc
                    });
            }

            deletions.AddRange(holders.Select(h => (h.Scope, key)));
            promoted.Add(key);
        }

        return new Plan(writes, deletions, promoted, skipped);
    }

    /// <summary>
    /// Si el preset pinta esta clave. Las de una sección personalizada
    /// (section.{id} con un id que no está en la definición) sólo las usa
    /// el preset que tiene esa sección; las demás, cualquiera.
    /// </summary>
    private static bool UsesKey(
        PresetInfo preset,
        string key,
        HashSet<string> definitionSections)
    {
        const string sectionPrefix = "section.";

        if (!key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        var sectionId = key[sectionPrefix.Length..];

        if (definitionSections.Contains(sectionId))
            return true;

        return preset.Configuration.Sections.Any(x =>
            string.Equals(x.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> DefinitionSectionIds(
        ReportDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(IEnumerable<ReportSectionDefinition> sections)
        {
            foreach (var section in sections)
            {
                ids.Add(section.Id);
                Walk(section.Sections);
            }
        }

        Walk(definition.Sections);

        return ids;
    }
}
