namespace Renderset.Core.Rendering;

/// <summary>
/// Error de validación con el detalle que necesita quien integra. Devolver
/// "faltan datos" no sirve: hay que decir qué path falta y qué se esperaba.
/// </summary>
public sealed class RenderValidationError
{
    public required string Code { get; set; }

    public required string Message { get; set; }

    /// <summary>
    /// Path del modelo o nombre de columna afectado.
    /// </summary>
    public string? Path { get; set; }

    public string? Expected { get; set; }

    public string? Actual { get; set; }
}
