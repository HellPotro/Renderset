namespace Renderset.Core.Localization;

public sealed class ReportResource
{
    /// <summary>
    /// Ámbito del recurso: <see cref="ReportResourceScope.Global"/> para el
    /// diccionario común del tenant, o el identificador del report.
    /// </summary>
    public required string Scope { get; set; }

    public required string Key { get; set; }

    /// <summary>
    /// Cultura en formato BCP-47 (es-ES, en-GB).
    /// </summary>
    public required string Culture { get; set; }

    /// <summary>
    /// Texto traducido. Nulo significa que la clave existe pero está
    /// pendiente de traducir en esta cultura.
    /// </summary>
    public string? Value { get; set; }

    public ReportResourceSource Source { get; set; } = ReportResourceSource.Manual;

    public string? Description { get; set; }

    public string? TranslationProvider { get; set; }

    public DateTime? MachineTranslatedAtUtc { get; set; }

    public bool NeedsReview { get; set; }
}
