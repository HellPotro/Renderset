public sealed class ResolvedReportFooter
{
    public const string DefaultPageNumberFormat = "Página {page} de {pages}";

    public bool Visible { get; set; }

    public string? Text { get; set; }

    public bool ShowGenerationDate { get; set; }

    public bool ShowPageNumber { get; set; }

    /// <summary>
    /// Texto de la numeración ya traducido, con los marcadores {page} y
    /// {pages}. Sólo se usa con <see cref="ShowPageNumber"/>.
    /// </summary>
    public string PageNumberFormat { get; set; } = DefaultPageNumberFormat;

    /// <summary>
    /// Hay algo que pintar: texto, fecha o numeración.
    /// </summary>
    public bool HasContent =>
        Visible &&
        (!string.IsNullOrWhiteSpace(Text) ||
         ShowGenerationDate ||
         ShowPageNumber);
}
