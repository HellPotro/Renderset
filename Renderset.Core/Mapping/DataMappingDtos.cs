using System.Text.Json;
using Renderset.Core.DataSources;

namespace Renderset.Core.Mapping;

/// <summary>
/// Respuesta al guardar: el mapping tal como ha quedado y lo que no encaja
/// con el report al que está asignado.
/// </summary>
public sealed class DataMappingSaveResponse
{
    public required DataMapping Mapping { get; init; }

    public IReadOnlyList<DataMappingCompatibilityIssue> Issues { get; init; } = [];
}

public sealed class DataMappingPreviewRequest
{
    public required TabularPayload Rows { get; init; }

    /// <summary>
    /// Cuántos documentos devolver como mucho. El recuento es siempre el total.
    /// </summary>
    public int Take { get; init; } = 3;
}

public sealed class DataMappingPreviewResponse
{
    public int DocumentCount { get; init; }

    public IReadOnlyList<JsonElement> Documents { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];
}
