namespace Renderset.Core.Variables;

public interface IReportVariableResolver
{
    /// <summary>
    /// Sustituye los marcadores del texto por el valor de las variables del
    /// tenant.
    /// </summary>
    /// <param name="culture">
    /// Idioma con el que resolver las variables traducibles. Nulo usa el
    /// idioma por defecto del tenant, que es el comportamiento que había
    /// antes de que las variables pudieran traducirse.
    /// </param>
    Task<string> ResolveAsync(
        string tenantId,
        string input,
        string? culture = null,
        CancellationToken cancellationToken = default);
}
