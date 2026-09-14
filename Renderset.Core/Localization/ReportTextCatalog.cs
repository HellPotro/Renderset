namespace Renderset.Core.Localization;

/// <summary>
/// Instantánea inmutable del diccionario para una cultura efectiva concreta.
/// Los ámbitos (global, report, preset) y el fallback entre culturas ya se
/// han aplanado al construirlo, de modo que resolver es una búsqueda directa.
/// Esto mantiene <see cref="Services.ReportConfigurationResolver"/> síncrono,
/// puro y testeable sin base de datos.
/// </summary>
public sealed class ReportTextCatalog
{
    private readonly IReadOnlyDictionary<string, string> _entries;

    public ReportTextCatalog(
        IReadOnlyDictionary<string, string> entries,
        string culture = "")
    {
        _entries = entries;
        Culture = culture;
    }

    public static ReportTextCatalog Empty { get; } =
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public string Culture { get; }

    public int Count => _entries.Count;

    /// <summary>
    /// Devuelve el texto de la clave, o <paramref name="fallback"/> si la
    /// clave es nula, no existe o está sin traducir. Nunca lanza: un informe
    /// con una traducción pendiente debe seguir emitiéndose.
    /// </summary>
    public string Resolve(
        string? key,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback;

        return _entries.TryGetValue(key, out var value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    public bool Contains(
        string key)
    {
        return _entries.ContainsKey(key);
    }
}
