using Renderset.Core.Rendering;

namespace Renderset.Core.Sharing;

/// <summary>
/// Resultado de crear un bundle o renovar su enlace. Es el único momento en
/// que existe el token en claro: después sólo queda su hash.
/// </summary>
public sealed class DocumentBundleLinkResult
{
    public DocumentBundle? Bundle { get; init; }

    public string? Token { get; init; }

    public bool NotFound { get; init; }

    public IReadOnlyList<RenderValidationError> Errors { get; init; } = [];

    public bool Succeeded =>
        Bundle is not null &&
        Token is not null &&
        Errors.Count == 0;

    public static DocumentBundleLinkResult Success(
        DocumentBundle bundle,
        string token) =>
        new()
        {
            Bundle = bundle,
            Token = token
        };

    public static DocumentBundleLinkResult Failure(
        IReadOnlyList<RenderValidationError> errors) =>
        new()
        {
            Errors = errors
        };

    public static DocumentBundleLinkResult Missing() =>
        new()
        {
            NotFound = true
        };
}


/// <summary>
/// Resultado de abrir un enlace público.
///
/// Un token incorrecto da <see cref="DocumentBundleOpenStatus.NotFound"/>,
/// igual que un bundle que no existe: distinguirlos le diría a quien prueba
/// enlaces que ha acertado el Guid.
/// </summary>
public sealed class DocumentBundleOpenResult
{
    public DocumentBundleOpenStatus Status { get; init; }

    /// <summary>
    /// Informado también cuando ha caducado o se ha revocado: el token era
    /// correcto, así que se puede enseñar la página de aviso con la marca
    /// del tenant.
    /// </summary>
    public DocumentBundle? Bundle { get; init; }
}


public enum DocumentBundleOpenStatus
{
    Available,
    NotFound,
    Expired,
    Revoked
}


public sealed class BundledDocument
{
    public required DocumentBundleItem Item { get; init; }

    public required RenderedDocument Document { get; init; }
}
