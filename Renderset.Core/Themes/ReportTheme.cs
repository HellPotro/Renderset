namespace Renderset.Core.Themes;

/// <summary>
/// Colores y tipografía del documento.
///
/// Vive en dos sitios: como tema de empresa del tenant (tabla ReportThemes,
/// pantalla "Temas") y como copia dentro de cada preset. El preset que
/// referencia un tema de empresa (ReportConfiguration.ThemeId) se pinta con
/// el tema vivo; su copia sólo se usa si el tema se ha borrado, o si el
/// preset es "personalizado" y no referencia ninguno.
///
/// Las propiedades anulables son posteriores al resto: nulo pinta como antes
/// de existir, así que los presets guardados no cambian de aspecto.
/// </summary>
public sealed class ReportTheme
{
    public const string DefaultId = "default";

    public string Id { get; set; } = DefaultId;

    public string Name { get; set; } = "Default";

    public string PrimaryColor { get; set; } = "#00285A";

    public string SecondaryColor { get; set; } = "#E8EFF3";

    public string TextColor { get; set; } = "#25313A";

    public string MutedTextColor { get; set; } = "#6D7B83";

    public string BorderColor { get; set; } = "#D8E1E7";

    public string SectionBackgroundColor { get; set; } = "#E8EFF3";

    public string SectionTextColor { get; set; } = "#00285A";

    public string TableHeaderBackgroundColor { get; set; } = "#E8EFF3";

    public string TableHeaderTextColor { get; set; } = "#00285A";

    public string BackgroundColor { get; set; } = "#FFFFFF";

    public string FontFamily { get; set; }
        = "\"Segoe UI\", Arial, sans-serif";

    /// <summary>
    /// Color del título de la cabecera. Nulo = color de texto.
    /// </summary>
    public string? TitleColor { get; set; }

    /// <summary>
    /// Fondo de las filas pares de las tablas. Nulo = sin bandas.
    /// </summary>
    public string? TableStripeColor { get; set; }

    /// <summary>
    /// Fondo de las etiquetas de los campos en modo lista. Nulo = el gris
    /// claro de siempre.
    /// </summary>
    public string? FieldLabelBackgroundColor { get; set; }

    public ReportTheme Clone() =>
        (ReportTheme)MemberwiseClone();
}
