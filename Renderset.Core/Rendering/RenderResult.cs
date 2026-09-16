namespace Renderset.Core.Rendering;

/// <summary>
/// Resultado de una generación. Los errores son de validación y llevan el
/// detalle que necesita quien integra, no un "no se ha podido generar".
/// </summary>
public sealed class RenderResult
{
    public RenderedDocument? Document { get; init; }

    public IReadOnlyList<RenderValidationError> Errors { get; init; } = [];

    public bool Succeeded =>
        Document is not null &&
        Errors.Count == 0;

    public static RenderResult Success(
        RenderedDocument document)
    {
        return new RenderResult
        {
            Document = document
        };
    }

    public static RenderResult Failure(
        string code,
        string message,
        string? path = null,
        string? expected = null,
        string? actual = null)
    {
        return new RenderResult
        {
            Errors =
            [
                new RenderValidationError
                {
                    Code = code,
                    Message = message,
                    Path = path,
                    Expected = expected,
                    Actual = actual
                }
            ]
        };
    }
}
