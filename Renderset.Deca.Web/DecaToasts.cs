using System.Net;
using System.Text.Json;
using Refit;
using Renderset.Blazor.Components.Toasts;

namespace Renderset.Deca.Web;

/// <summary>
/// Llamadas a la API con el error en un toast, como en Renderset.Web
/// (ToastServiceExtensions vive en el host y esta librería no lo ve). Los
/// errores de la API llegan como [{ code, path, message }].
/// </summary>
public static class DecaToasts
{
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

    public static string Describe(
        Exception exception)
    {
        if (exception is not ApiException api)
            return exception.Message;

        var content = api.Content?.Trim();

        if (!string.IsNullOrWhiteSpace(content) && content.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(content);

                var messages =
                    document.RootElement
                        .EnumerateArray()
                        .Select(x =>
                            x.ValueKind == JsonValueKind.Object &&
                            x.TryGetProperty("message", out var message) &&
                            message.ValueKind == JsonValueKind.String
                                ? message.GetString()
                                : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                if (messages.Count > 0)
                    return string.Join(" · ", messages);
            }
            catch (JsonException)
            {
            }
        }

        return api.StatusCode switch
        {
            HttpStatusCode.NotFound => "No se ha encontrado el DeCA.",
            HttpStatusCode.Conflict => "Alguien ha cambiado este DeCA a la vez. Recarga la página.",
            HttpStatusCode.ServiceUnavailable => "El servicio de PDF no está disponible: el DeCA no se ha emitido. Inténtalo en unos minutos.",
            _ => $"Error {(int)api.StatusCode} al llamar a la API."
        };
    }
}
