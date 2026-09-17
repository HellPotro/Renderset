namespace Renderset.Blazor.Components.Editor;

/// <summary>
/// Un elemento del cuerpo visto desde la pestaña de disposición: sólo lo que
/// hace falta para colocarlo.
///
/// Se resuelve en el editor y no aquí porque el nombre sale de tres sitios
/// distintos según el tipo (sección resuelta, bloque local o bloque
/// reutilizable) y esa búsqueda ya vive allí.
/// </summary>
public sealed record ReportLayoutItem(
    string Id,
    string Label,
    string Kind,
    string Icon,
    bool? SameRow,
    int? Span);
