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

    public ReportTextWorkspace(
        string scope,
        string culture,
        IEnumerable<ReportResource>? ownValues = null,
        IReadOnlyDictionary<string, string>? inherited = null)
    {
        Scope = scope;
        Culture = culture;

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
    }

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

        return new ReportTextCatalog(
            entries,
            Culture);
    }
}
