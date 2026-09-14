namespace Renderset.Core.Configurations;

public sealed class ReportTextBlockConfiguration
{
    /// <summary>
    /// Clave de diccionario del título. Si está informada, gana sobre
    /// <see cref="Title"/>, que queda como semilla en la cultura base.
    /// </summary>
    public string? TitleKey { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Clave de diccionario del cuerpo. El texto resuelto puede contener
    /// variables {{clave}}: primero se traduce la plantilla y después se
    /// sustituyen las variables, nunca al revés.
    /// </summary>
    public string? TextKey { get; set; }

    public string Text { get; set; } = string.Empty;

    public ReportTextBlockStyle Style { get; set; } = ReportTextBlockStyle.Default;
}

public enum ReportTextBlockStyle
{
    Default,
    Information,
    Warning,
    Success
}
