using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

/// <summary>
/// Una línea de datos de la cabecera: razón social, CIF, teléfono, web...
///
/// El texto no se guarda aquí, sino en el diccionario bajo
/// <see cref="TextKey"/>, igual que el título y el subtítulo. Dentro del texto
/// pueden ir {{variables}} del tenant, que es donde vive el dato real.
/// </summary>
public sealed class ReportHeaderLineConfiguration
{
    /// <summary>
    /// Identificador estable de la línea. Es lo que hace que la clave del
    /// diccionario sobreviva a reordenaciones y ediciones.
    /// </summary>
    public string Id { get; set; } = default!;

    public string? TextKey { get; set; }

    public ReportHeaderLineStyle Style { get; set; } =
        ReportHeaderLineStyle.Normal;

    public int Order { get; set; }
}
