namespace Renderset.Core.Rendering;

public sealed class RenderContext
{
    /// <summary>
    /// BCP-47 completo. Decide el diccionario y también el formato de fechas,
    /// números y divisa.
    /// </summary>
    public string? Culture { get; set; }

    public string? Currency { get; set; }

    public string? TimeZone { get; set; }

    /// <summary>
    /// Clave de contexto para resolver el preset por assignment: el código de
    /// cliente, el país, lo que toque.
    /// </summary>
    public string? ContextType { get; set; }

    public string? ContextKey { get; set; }
}
