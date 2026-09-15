namespace Renderset.Blazor.Components.Toasts;

/// <summary>
/// Se registra como <c>Scoped</c>. En Blazor Server eso significa uno por
/// circuito, es decir, por pestaña de un usuario: registrarlo como singleton
/// mostraría los mensajes de un usuario a todos los demás.
/// </summary>
public sealed class ToastService
    : IToastService
{
    private const int MaxVisible = 5;

    private static readonly TimeSpan DefaultDuration =
        TimeSpan.FromSeconds(4);

    private readonly List<ToastMessage> _messages = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToList();
            }
        }
    }

    public event Action? Changed;

    public void Show(
        ToastLevel level,
        string text,
        string? title = null,
        TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        ToastMessage message;

        lock (_gate)
        {
            // Mismo nivel y mismo texto: se suma al que ya está en pantalla.
            var existing =
                _messages.FirstOrDefault(x =>
                    x.Level == level &&
                    string.Equals(x.Text, text, StringComparison.Ordinal));

            if (existing is not null)
            {
                existing.Count++;

                Changed?.Invoke();

                return;
            }

            message =
                new ToastMessage
                {
                    Level = level,
                    Text = text,
                    Title = title,

                    Duration = duration ?? DefaultFor(level)
                };

            _messages.Add(message);

            while (_messages.Count > MaxVisible)
                _messages.RemoveAt(0);
        }

        Changed?.Invoke();

        if (message.Duration is { } lifetime)
            _ = ExpireAsync(message.Id, lifetime);
    }

    public void Success(
        string text,
        string? title = null) =>
        Show(ToastLevel.Success, text, title);

    public void Error(
        string text,
        string? title = null) =>
        Show(ToastLevel.Error, text, title);

    public void Warning(
        string text,
        string? title = null) =>
        Show(ToastLevel.Warning, text, title);

    public void Info(
        string text,
        string? title = null) =>
        Show(ToastLevel.Info, text, title);

    public void Remove(
        Guid id)
    {
        lock (_gate)
        {
            var message =
                _messages.FirstOrDefault(x => x.Id == id);

            if (message is null)
                return;

            _messages.Remove(message);
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_messages.Count == 0)
                return;

            _messages.Clear();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Los errores y los avisos no se cierran solos: son justo los que el
    /// usuario necesita leer entero, y a veces copiar.
    /// </summary>
    private static TimeSpan? DefaultFor(
        ToastLevel level) =>
        level switch
        {
            ToastLevel.Error => null,
            ToastLevel.Warning => TimeSpan.FromSeconds(8),
            _ => DefaultDuration
        };

    private async Task ExpireAsync(
        Guid id,
        TimeSpan delay)
    {
        await Task.Delay(delay);

        Remove(id);
    }
}
