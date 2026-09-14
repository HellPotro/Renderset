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
}
