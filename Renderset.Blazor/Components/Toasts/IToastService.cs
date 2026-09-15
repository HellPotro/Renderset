namespace Renderset.Blazor.Components.Toasts;

public interface IToastService
{
    IReadOnlyList<ToastMessage> Messages { get; }

    event Action? Changed;

    void Show(
        ToastLevel level,
        string text,
        string? title = null,
        TimeSpan? duration = null);

    void Success(
        string text,
        string? title = null);

    void Error(
        string text,
        string? title = null);

    void Warning(
        string text,
        string? title = null);

    void Info(
        string text,
        string? title = null);

    void Remove(
        Guid id);

    void Clear();
}
