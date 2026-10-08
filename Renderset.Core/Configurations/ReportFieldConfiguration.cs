using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportFieldConfiguration
{
    public string FieldId { get; set; } = default!;

    public bool? Visible { get; set; }

    public int? Order { get; set; }

    public string? LabelKey { get; set; }

    /// <summary>
    /// Cambia cómo se pinta el valor (texto, número, fecha…) sin tocar la
    /// definición inferida. Nulo = el tipo que se infirió de los datos.
    /// </summary>
    public ReportFieldType? Type { get; set; }
}