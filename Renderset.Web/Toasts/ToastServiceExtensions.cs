using Refit;
using Renderset.Blazor.Components.Toasts;

namespace Renderset.Web.Toasts;

/// <summary>
/// Envuelve las llamadas a la API para que ninguna página tenga que repetir
/// el try/catch con su propio campo <c>_error</c>.
///
/// Está aquí y no en Renderset.Blazor porque Refit sólo se referencia desde
/// Renderset.Web: el RCL no debe saber cómo se habla con el backend.
/// </summary>
public static class ToastServiceExtensions
{
    /// <summary>
    /// Ejecuta la operación y notifica el resultado. Devuelve true si fue
    /// bien, para poder encadenar (navegar, recargar) sólo en ese caso.
    /// </summary>
    public static async Task<bool> RunAsync(
        this IToastService toasts,
        Func<Task> operation,
        string? success = null,
        string? errorTitle = null)
    {
        try
        {
            await operation();

            if (!string.IsNullOrWhiteSpace(success))
                toasts.Success(success);

            return true;
        }
        catch (Exception exception)
        {
            toasts.Error(
                Describe(exception),
                errorTitle ?? "No se ha podido completar la operación");

            return false;
        }
    }

    /// <summary>
    /// Igual que la anterior pero devolviendo el resultado. Ante error
    /// devuelve default, de modo que el llamante comprueba null.
    /// </summary>
    public static async Task<T?> RunAsync<T>(
        this IToastService toasts,
        Func<Task<T>> operation,
        string? success = null,
        string? errorTitle = null)
    {
        try
        {
            var result = await operation();

            if (!string.IsNullOrWhiteSpace(success))
                toasts.Success(success);

            return result;
        }
        catch (Exception exception)
        {
            toasts.Error(
                Describe(exception),
                errorTitle ?? "No se ha podido completar la operación");

            return default;
        }
    }

    /// <summary>
    /// "Response status code does not indicate success: 400 (Bad Request)" no
    /// le dice nada a nadie. Los endpoints devuelven el motivo en el cuerpo
    /// (Results.BadRequest("El presetId de la URL no coincide...")), así que
    /// es ese texto el que hay que enseñar.
    /// </summary>
    public static string Describe(
        Exception exception)
    {
        if (exception is not ApiException apiException)
            return exception.Message;

        var content = apiException.Content?.Trim();

        if (!string.IsNullOrWhiteSpace(content) &&
            !content.StartsWith('{') &&
            !content.StartsWith('<'))
        {
            return content;
        }

        return apiException.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound =>
                "No se ha encontrado el recurso.",

            System.Net.HttpStatusCode.Conflict =>
                "Alguien ha modificado estos datos mientras los editabas. " +
                "Recarga antes de volver a guardar.",

            System.Net.HttpStatusCode.BadRequest =>
                "La petición no es válida.",

            _ =>
                $"Error {(int)apiException.StatusCode} al llamar a la API."
        };
    }
}
