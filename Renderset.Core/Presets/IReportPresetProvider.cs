using System.Text.Json;
using Renderset.Core.Persistence;

namespace Renderset.Core.Presets;

public interface IReportPresetProvider
{
    Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType = null,
        string? contextKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual, pero el contexto puede salir de los datos del documento: un
    /// assignment cuyo tipo de contexto es la ruta de un campo
    /// ("cliente.codigo") se aplica cuando ese campo vale su clave. Así el
    /// ERP no tiene que mandar context.contextType/contextKey.
    ///
    /// Orden: contexto explícito de la petición, contexto sacado de los
    /// datos y, por último, el preset por defecto del report.
    /// </summary>
    Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType,
        string? contextKey,
        JsonElement data,
        CancellationToken cancellationToken = default);
}
