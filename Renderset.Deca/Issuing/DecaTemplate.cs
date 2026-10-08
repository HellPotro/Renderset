using Renderset.Core.Configurations;
using Renderset.Core.Themes;

namespace Renderset.Deca.Issuing;

/// <summary>
/// Diseño del DeCA de un tenant: lo mismo que guarda un preset de RenderSet
/// (configuración sparse, tema y textos) sobre la definición fija de
/// <see cref="DecaReportTemplate"/>. Se edita con el ReportEditor de
/// RenderSet en el portal.
///
/// Cada guardado es una versión nueva (no se sobrescribe): cada DeCA anota
/// con qué versión del diseño se emitió.
/// </summary>
public sealed class DecaTemplate
{
    /// <summary>
    /// Versión 0 = el diseño base (nunca se ha personalizado o se ha
    /// restablecido).
    /// </summary>
    public const int BaseVersion = 0;

    public required string TenantId { get; init; }

    public int Version { get; init; }

    /// <summary>
    /// Nulo = el diseño base.
    /// </summary>
    public ReportConfiguration? Configuration { get; init; }

    public ReportTheme? Theme { get; init; }

    /// <summary>
    /// Textos del diseño (etiquetas, título, pie) por clave del diccionario.
    /// </summary>
    public Dictionary<string, string> Texts { get; init; } = [];

    public DateTime UpdatedAtUtc { get; init; }

    public string? UpdatedBy { get; init; }

    public bool IsBase =>
        Configuration is null;
}

/// <summary>
/// Diseños por tenant, en la base de datos Deca. Sólo se insertan versiones.
/// </summary>
public interface IDecaTemplateRepository
{
    /// <summary>
    /// La versión vigente. Nulo si el tenant nunca ha tocado el diseño.
    /// </summary>
    Task<DecaTemplate?> GetCurrentAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda una versión nueva (la vigente + 1). Falso si otro ha guardado
    /// entretanto (<paramref name="expectedVersion"/> ya no es la vigente).
    /// Con <see cref="DecaTemplate.Configuration"/> nulo, restablece el base.
    /// </summary>
    Task<bool> AddVersionAsync(
        DecaTemplate template,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
