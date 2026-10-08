namespace Renderset.Core.Themes;

/// <summary>
/// Familias tipográficas que se ofrecen en los editores.
///
/// Hay dos clases:
///
///   - Del sistema: pilas con alternativas seguras. El PDF sale de Chromium
///     en Linux, donde Segoe UI o Calibri no existen y se cae a la siguiente
///     de la lista, así que en el PDF pueden no verse igual que en pantalla.
///
///   - Incluidas en RenderSet (<see cref="WebFonts"/>): tipografías libres
///     (SIL Open Font License) que viajan con la aplicación en
///     Renderset.Blazor/wwwroot/fonts. El documento emitido lleva incrustada
///     la que usa (latín, normal y negrita), así que se ve igual en pantalla,
///     en el PDF y en un HTML guardado o enviado por correo, sin depender de
///     lo que tenga instalado nadie.
///
/// Añadir una: copiar sus .woff2 (latin-400 y latin-700) a wwwroot/fonts
/// con el nombre {fichero}-latin-400-normal.woff2 / -700-, añadirla a
/// <see cref="WebFonts"/> y su @font-face a css/report-fonts.css.
/// </summary>
public static class ReportThemeFonts
{
    public sealed record WebFont(
        string Family,
        string File,
        string Fallback)
    {
        public string Stack =>
            $"\"{Family}\", {Fallback}";
    }

    private const string Sans = "\"Segoe UI\", Arial, sans-serif";
    private const string Serif = "Georgia, \"Times New Roman\", serif";
    private const string Mono = "Consolas, \"Liberation Mono\", monospace";

    public static IReadOnlyList<WebFont> WebFonts { get; } =
    [
        new("Inter", "inter", Sans),
        new("Roboto", "roboto", Sans),
        new("Open Sans", "open-sans", Sans),
        new("Lato", "lato", Sans),
        new("Montserrat", "montserrat", Sans),
        new("Source Sans 3", "source-sans-3", Sans),
        new("IBM Plex Sans", "ibm-plex-sans", Sans),
        new("Nunito Sans", "nunito-sans", Sans),
        new("Poppins", "poppins", Sans),
        new("Barlow", "barlow", Sans),
        new("Merriweather", "merriweather", Serif),
        new("Roboto Mono", "roboto-mono", Mono)
    ];

    public static IReadOnlyList<(string Name, string Stack)> SystemFonts { get; } =
    [
        ("Segoe UI", "\"Segoe UI\", Arial, sans-serif"),
        ("Arial", "Arial, \"Helvetica Neue\", Helvetica, sans-serif"),
        ("Helvetica", "\"Helvetica Neue\", Helvetica, Arial, sans-serif"),
        ("Calibri", "Calibri, Carlito, Arial, sans-serif"),
        ("Verdana", "Verdana, Geneva, sans-serif"),
        ("Tahoma", "Tahoma, Verdana, sans-serif"),
        ("Georgia", "Georgia, \"Times New Roman\", serif"),
        ("Times New Roman", "\"Times New Roman\", Times, serif"),
        ("Monoespaciada", "Consolas, \"Liberation Mono\", \"Courier New\", monospace")
    ];

    /// <summary>
    /// Todas, primero las del sistema (las de siempre) y después las
    /// incluidas.
    /// </summary>
    public static IReadOnlyList<(string Name, string Stack)> All { get; } =
        SystemFonts
            .Concat(WebFonts.Select(x => (x.Family, x.Stack)))
            .ToList();

    /// <summary>
    /// Tipografía incluida que usa una pila CSS, mirando la primera familia
    /// ("Inter", "Segoe UI", ... → Inter). Nula si es del sistema.
    /// </summary>
    public static WebFont? FindWebFont(
        string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
            return null;

        var first =
            fontFamily
                .Split(',', 2)[0]
                .Trim()
                .Trim('"', '\'')
                .Trim();

        return WebFonts.FirstOrDefault(x =>
            string.Equals(
                x.Family,
                first,
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsWebFont(
        string? fontFamily) =>
        FindWebFont(fontFamily) is not null;
}
