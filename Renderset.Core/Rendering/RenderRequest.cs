using System.Text.Json;
using Renderset.Core.DataSources;

namespace Renderset.Core.Rendering;

/// <summary>
/// Petición de generación. Admite las tres formas en que pueden llegar los
/// datos, y exactamente una de ellas debe venir informada.
/// </summary>
public sealed class RenderRequest
{
    public required string ReportId { get; set; }

    /// <summary>
    /// Opcional: si no viene, se resuelve por assignment usando
    /// <see cref="Context"/>.
    /// </summary>
    public string? PresetId { get; set; }

    /// <summary>
    /// Opcional: fija la versión del preset para poder reemitir un documento
    /// antiguo con el diseño que tenía entonces.
    /// </summary>
    public int? PresetVersion { get; set; }

    /// <summary>
    /// Necesario cuando se envía <see cref="Rows"/>: indica cómo convertir la
    /// tabla plana en el modelo jerárquico. No hace falta con
    /// <see cref="Data"/>, que ya viene jerárquico.
    /// </summary>
    public string? MappingId { get; set; }

    /// <summary>
    /// Datos ya jerárquicos. Es el modo que existe hoy.
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Datos planos tal como salen de un SELECT con JOINs.
    /// </summary>
    public TabularPayload? Rows { get; set; }

    public RenderContext Context { get; set; } = new();

    public RenderOutput Output { get; set; } = new();

    /// <summary>
    /// Evita duplicados si el cliente reintenta por timeout de red. Misma
    /// clave, mismo documento, sin volver a generarlo.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public RenderRequestMode ResolveMode()
    {
        var provided =
            (Data.HasValue ? 1 : 0) +
            (Rows is not null ? 1 : 0);

        return provided switch
        {
            0 => RenderRequestMode.None,
            > 1 => RenderRequestMode.Ambiguous,
            _ => Data.HasValue
                ? RenderRequestMode.Hierarchical
                : RenderRequestMode.Tabular
        };
    }
}

public enum RenderRequestMode
{
    None,
    Ambiguous,
    Hierarchical,
    Tabular
}
