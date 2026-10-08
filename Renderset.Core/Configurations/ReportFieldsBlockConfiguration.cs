using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

/// <summary>
/// Bloque de campos fijos: una lista de etiqueta + valor que no sale de los
/// datos del documento sino del propio bloque. Lo típico son los datos de
/// empresa (razón social, CIF, dirección, teléfono) o unas condiciones fijas.
///
/// Se pinta igual que una sección de campos (lista o cuadrícula) para que en
/// el documento no se note la diferencia.
///
/// Los textos no viven aquí sino en el diccionario común, bajo la clave de
/// cada campo (<see cref="Localization.ReportTextKeys.BlockFieldLabel"/> y
/// <see cref="Localization.ReportTextKeys.BlockFieldValue"/>). Los valores
/// admiten {{variables}} del tenant y {{data.ruta}} del documento.
/// </summary>
public sealed class ReportFieldsBlockConfiguration
{
    /// <summary>
    /// Clave del título del bloque. Sin título no se pinta la banda.
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>
    /// Nulo = se muestra el título si lo hay.
    /// </summary>
    public bool? ShowTitle { get; set; }

    /// <summary>
    /// Lista o cuadrícula, con la misma regla que las secciones
    /// (<see cref="ReportSectionGrid.Normalize"/>).
    /// </summary>
    public ReportSectionLayout? Layout { get; set; }

    public int? Columns { get; set; }

    public List<ReportFixedFieldConfiguration> Fields { get; set; } = [];
}

public sealed class ReportFixedFieldConfiguration
{
    /// <summary>
    /// Identificador estable: de él cuelgan las claves del diccionario, así
    /// que reordenar o renombrar no pierde traducciones.
    /// </summary>
    public string Id { get; set; } = default!;

    public string? LabelKey { get; set; }

    public string? ValueKey { get; set; }

    public int Order { get; set; }
}
