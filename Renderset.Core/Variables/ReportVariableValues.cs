using Renderset.Core.Localization;

namespace Renderset.Core.Variables;

/// <summary>
/// Valores de las variables del tenant para una cultura concreta.
///
/// Existe como servicio propio porque hacen falta en dos sitios que no se
/// conocen entre sí: el render de un documento y el endpoint que sirve un
/// bloque ya resuelto. Tener la lógica dos veces era la forma segura de que
/// el bloque del editor y el PDF acabaran diciendo cosas distintas.
/// </summary>
public interface IReportVariableValues
{
    Task<IReadOnlyDictionary<string, string>> GetAsync(
        string tenantId,
        string? culture,
        CancellationToken cancellationToken = default);
}


public sealed class ReportVariableValues
    : IReportVariableValues
{
    private readonly IReportVariableRepository _variables;
    private readonly IReportResourceRepository _resources;
    private readonly ITenantCultureRepository _cultures;

    public ReportVariableValues(
        IReportVariableRepository variables,
        IReportResourceRepository resources,
        ITenantCultureRepository cultures)
    {
        _variables = variables;
        _resources = resources;
        _cultures = cultures;
    }


    public async Task<IReadOnlyDictionary<string, string>> GetAsync(
        string tenantId,
        string? culture,
        CancellationToken cancellationToken = default)
    {
        var variables =
            await _variables.GetAllAsync(
                tenantId,
                cancellationToken);

        var values = ReportVariableTemplate.ToDictionary(variables);

        // Sin ninguna variable traducible no se toca el diccionario: es el
        // caso habitual y no tiene por qué pagar una consulta extra.
        if (!variables.Any(x => x.Translatable))
            return values;

        var defaultCulture =
            await _cultures.GetDefaultAsync(
                tenantId,
                cancellationToken);

        // Misma cadena de reserva que el catálogo de textos (en-GB → en →
        // idioma por defecto), para que una variable y una etiqueta de la
        // misma pantalla no caigan en idiomas distintos.
        var chain =
            ReportTextCatalogFactory.BuildCultureChain(
                culture,
                defaultCulture?.Culture);

        if (chain.Count == 0)
            return values;

        var resources =
            await _resources.GetByScopesAsync(
                tenantId,
                [ReportResourceScope.Global],
                cancellationToken);

        var translations = Index(resources);

        foreach (var variable in variables)
        {
            if (!variable.Translatable ||
                string.IsNullOrWhiteSpace(variable.Key))
            {
                continue;
            }

            var translated =
                FirstMatch(
                    translations,
                    ReportTextKeys.Variable(variable.Key),
                    chain);

            // Si no hay traducción en ninguna cultura de la cadena se queda
            // el valor de la variable. Vaciar el marcador dejaría un hueco
            // en el documento sin explicar por qué.
            if (!string.IsNullOrWhiteSpace(translated))
                values[variable.Key] = translated;
        }

        return values;
    }


    private static Dictionary<string, Dictionary<string, string>> Index(
        IReadOnlyCollection<ReportResource> resources)
    {
        var index =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Value))
                continue;

            if (!index.TryGetValue(resource.Key, out var byCulture))
            {
                byCulture =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                index[resource.Key] = byCulture;
            }

            byCulture[resource.Culture] = resource.Value;
        }

        return index;
    }


    private static string? FirstMatch(
        IReadOnlyDictionary<string, Dictionary<string, string>> translations,
        string key,
        IReadOnlyList<string> chain)
    {
        if (!translations.TryGetValue(key, out var byCulture))
            return null;

        foreach (var culture in chain)
        {
            if (byCulture.TryGetValue(culture, out var value))
                return value;
        }

        return null;
    }
}
