namespace Renderset.Blazor.Components.Toasts;

public sealed class ToastMessage
{
    public Guid Id { get; } = Guid.NewGuid();

    public required ToastLevel Level { get; init; }

    public required string Text { get; init; }

    public string? Title { get; init; }

    /// <summary>
    /// Nulo significa que no se cierra solo. Los errores nacen así a
    /// propósito: un mensaje que explica por qué ha fallado algo tiene que
    /// poder leerse sin prisa.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Veces que se ha repetido el mismo mensaje. En vez de apilar veinte
    /// toasts idénticos cuando algo falla en bucle, se muestra uno con
    /// contador.
    /// </summary>
    public int Count { get; set; } = 1;
}
