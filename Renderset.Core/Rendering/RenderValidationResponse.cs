namespace Renderset.Core.Rendering;

/// <summary>
/// Respuesta de POST /api/render/{tenantId}/validate: si la petición
/// emitiría un documento y, si no, por qué (los mismos errores que daría el
/// render). No se guarda nada.
/// </summary>
public sealed class RenderValidationResponse
{
    public bool Valid { get; init; }

    public required string ReportId { get; init; }

    /// <summary>
    /// Preset que se usaría (el pedido o el que resuelve el contexto).
    /// </summary>
    public string? PresetId { get; init; }

    public int? PresetVersion { get; init; }

    public IReadOnlyList<RenderValidationError> Errors { get; init; } = [];
}
