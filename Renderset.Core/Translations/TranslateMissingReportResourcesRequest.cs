namespace Renderset.Core.Translations;

public sealed class TranslateMissingReportResourcesRequest
{
    public required string Scope { get; set; }

    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    /// <summary>
    /// Si está activo, traduce también claves existentes en destino pero sin valor.
    /// </summary>
    public bool IncludeEmptyValues { get; set; } = true;

    /// <summary>
    /// Por seguridad, false por defecto. Si se activa, pisa traducciones existentes.
    /// </summary>
    public bool OverwriteExistingValues { get; set; }

    /// <summary>
    /// Limita la traducción a estas claves. Vacío o nulo traduce el ámbito
    /// entero, que es como se comportaba antes de existir este filtro.
    ///
    /// Sirve para traducir una fila suelta sin arrastrar el diccionario
    /// completo, que es lo que haría falta para eso si sólo se pudiera
    /// filtrar por ámbito.
    /// </summary>
    public List<string>? Keys { get; set; }
}
