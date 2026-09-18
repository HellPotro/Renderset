using Renderset.Core.Variables;

namespace Renderset.Core.Localization;

/// <summary>
/// Área de trabajo del diseñador sobre un ámbito concreto del diccionario
/// (normalmente <c>preset:&lt;presetId&gt;</c>) y una cultura de edición.
///
/// El editor no escribe texto literal en la configuración: escribe el valor
/// aquí, bajo la clave convencional del elemento, y la página anfitriona
/// persiste los cambios pendientes al guardar. Así un override de cliente
/// ("a esto llámale Albarán") queda como una entrada de diccionario
/// traducible, no como un literal en español metido en el preset.
/// </summary>
public sealed class ReportTextWorkspace
{
    private readonly Dictionary<string, string?> _own;
    private readonly Dictionary<string, string> _inherited;
    private readonly HashSet<string> _pending;
    private readonly Dictionary<string, string> _variables;

    public ReportTextWorkspace(
        string scope,
        string culture,
        IEnumerable<ReportResource>? ownValues = null,
        IReadOnlyDictionary<string, string>? inherited = null,
        IEnumerable<ReportVariable>? variables = null)
    {
        Scope = scope;
        Culture = culture;

        // Se materializa porque hay que recorrerla dos veces: para el
        // diccionario base y, más abajo, para traducir las que lo pidan.
        var declared =
            variables?.ToList() ?? [];

        _variables =
            ReportVariableTemplate.ToDictionary(declared);

        _own = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        _inherited = inherited is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                inherited,
                StringComparer.OrdinalIgnoreCase);

        _pending = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var resource in ownValues ?? [])
        {
            if (!string.Equals(
                    resource.Culture,
                    culture,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _own[resource.Key] = resource.Value;
        }

        ApplyTranslatedVariables(declared);
    }


    /// <summary>
    /// Sustituye el valor literal de las variables traducibles por su texto
    /// en la cultura de edición.
    ///
    /// Se hace aquí y no en la página para que el preview del diseñador y el
    /// documento final digan lo mismo: el render resuelve las variables
    /// contra el diccionario, y si el editor siguiera enseñando el literal,
    /// cambiar de idioma no se notaría hasta generar el PDF.
    ///
    /// Va después de cargar los valores propios porque necesita consultarlos.
    /// </summary>
    private void ApplyTranslatedVariables(
        IReadOnlyCollection<ReportVariable> variables)
    {
        foreach (var variable in variables)
        {
            if (!variable.Translatable ||
                string.IsNullOrWhiteSpace(variable.Key))
            {
                continue;
            }

            var key = ReportTextKeys.Variable(variable.Key);

            // Mismo orden que GetText: lo propio del ámbito manda sobre lo
            // heredado, y si no hay ninguno se queda el valor literal.
            if (_own.TryGetValue(key, out var own) &&
                !string.IsNullOrWhiteSpace(own))
            {
                _variables[variable.Key] = own;
                continue;
            }

            if (_inherited.TryGetValue(key, out var inherited) &&
                !string.IsNullOrWhiteSpace(inherited))
            {
                _variables[variable.Key] = inherited;
            }
        }
    }


    /// <summary>
    /// Valores de variables ya resueltos para esta cultura. Los usa el
    /// preview para no recalcularlos por su cuenta y acabar discrepando.
    /// </summary>
    public IReadOnlyDictionary<string, string> VariableValues => _variables;

    /// <summary>
    /// Área de trabajo sin persistencia, para usar el editor fuera de una
    /// página que sepa guardar recursos. Todo cae al literal de la definición.
    /// </summary>
    public static ReportTextWorkspace Detached()
    {
        return new ReportTextWorkspace(
            ReportResourceScope.Global,
            string.Empty);
    }

    public string Scope { get; }

    public string Culture { get; }

    public bool HasPendingChanges => _pending.Count > 0;

    /// <summary>
    /// Texto a mostrar: valor propio del ámbito, luego lo heredado de report
    /// y global, y por último el literal de la definición.
    /// </summary>
    public string GetText(
        string key,
        string fallback)
    {
        if (_own.TryGetValue(key, out var own) &&
            !string.IsNullOrWhiteSpace(own))
        {
            return own;
        }

        if (_inherited.TryGetValue(key, out var inherited) &&
            !string.IsNullOrWhiteSpace(inherited))
        {
            return inherited;
        }

        return fallback;
    }

    /// <summary>
    /// True si este ámbito pisa el valor heredado. Sirve para marcar en la UI
    /// qué etiquetas ha personalizado el cliente.
    /// </summary>
    public bool HasOwnValue(
        string key)
    {
        return _own.TryGetValue(key, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Guarda el valor en el ámbito. Vacío o nulo elimina el override y
    /// devuelve el elemento al texto heredado.
    /// </summary>
    public void SetText(
        string key,
        string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (_own.TryGetValue(key, out var current) &&
            string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _own[key] = normalized;
        _pending.Add(key);
    }

    public IReadOnlyCollection<ReportResource> GetPendingChanges()
    {
        return _pending
            .Select(key => new ReportResource
            {
                Scope = Scope,
                Key = key,
                Culture = Culture,
                Value = _own.TryGetValue(key, out var value)
                    ? value
                    : null,
                Source = ReportResourceSource.Manual
            })
            .ToList();
    }

    public void AcceptPendingChanges()
    {
        _pending.Clear();
    }

    /// <summary>
    /// Instantánea para el resolver, de modo que el preview refleje al
    /// instante lo que se está editando sin pasar por base de datos.
    /// </summary>
    public ReportTextCatalog ToCatalog()
    {
        var entries = new Dictionary<string, string>(
            _inherited,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _own)
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                entries.Remove(entry.Key);
                continue;
            }

            entries[entry.Key] = entry.Value;
        }

        /*
         * Las variables se sustituyen aquí y no en GetText a propósito.
         *
         * ToCatalog es lo que ve el render: ahí un texto como
         * "CIF: {{company.cif}}" tiene que llegar ya con el CIF puesto.
         * GetText es lo que ve el formulario de edición, y ahí hace falta
         * la plantilla: si al editor le enseñas el valor sustituido, lo
         * guarda como literal y el texto deja de seguir a la variable.
         */
        if (_variables.Count > 0)
        {
            foreach (var key in entries.Keys.ToList())
            {
                if (!ReportVariableTemplate.HasTokens(entries[key]))
                    continue;

                entries[key] =
                    ReportVariableTemplate.Apply(
                        entries[key],
                        _variables);
            }
        }

        return new ReportTextCatalog(
            entries,
            Culture);
    }
}
