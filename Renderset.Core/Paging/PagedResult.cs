namespace Renderset.Core.Paging;

/// <summary>
/// Una página de resultados con paginación por cursor.
///
/// No lleva total ni número de página a propósito: contar todas las filas
/// cuesta lo mismo que leerlas, y en un listado ordenado por fecha lo único
/// que hace falta saber es si hay más y cómo pedirlas.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Se pasa tal cual en la siguiente petición. Nulo cuando no hay más.
    /// </summary>
    public string? NextCursor { get; init; }

    public bool HasMore =>
        NextCursor is not null;

    public static PagedResult<T> Empty() =>
        new()
        {
            Items = []
        };
}
