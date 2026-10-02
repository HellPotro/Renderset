using Refit;
using Renderset.Blazor.Components.Toasts;

namespace Renderset.Web.Toasts;

/// <summary>
/// Envuelve las llamadas a la API para que las páginas no tengan que mantener
/// campos _error sólo para enseñar mensajes planos. Las validaciones de UI
/// siguen siendo de la página; cualquier fallo remoto pasa por ToastService.
/// </summary>
public static class ToastServiceExtensions
{
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

    public static async Task<bool> RunApiAsync(
        this IToastService toasts,
        Func<Task<IApiResponse>> operation,
        string? success = null,
        string? errorTitle = null)
    {
        try
        {
            var response = await operation();

            if (!response.IsSuccessStatusCode)
            {
                toasts.Error(
                    Describe(response),
                    errorTitle ?? "La API ha rechazado la operación");

                return false;
            }

            if (!string.IsNullOrWhiteSpace(success))
                toasts.Success(success);

            return true;
        }
        catch (Exception exception)
        {
            toasts.Error(
                Describe(exception),
                errorTitle ?? "No se ha podido llamar a la API");

            return false;
        }
    }

    public static async Task<T?> RunApiAsync<T>(
        this IToastService toasts,
        Func<Task<IApiResponse<T>>> operation,
        string? success = null,
        string? errorTitle = null)
    {
        try
        {
            var response = await operation();

            if (!response.IsSuccessStatusCode)
            {
                toasts.Error(
                    Describe(response),
                    errorTitle ?? "La API ha rechazado la operación");

                return default;
            }

            if (!string.IsNullOrWhiteSpace(success))
                toasts.Success(success);

            return response.Content;
        }
        catch (Exception exception)
        {
            toasts.Error(
                Describe(exception),
                errorTitle ?? "No se ha podido llamar a la API");

            return default;
        }
    }

    public static string Describe(
        IApiResponse response)
    {
        if (response.Error is not null)
            return Describe(response.Error);

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound =>
                "No se ha encontrado el recurso.",

            System.Net.HttpStatusCode.Conflict =>
                "Alguien ha modificado estos datos mientras los editabas. " +
                "Recarga antes de volver a guardar.",

            System.Net.HttpStatusCode.BadRequest =>
                "La petición no es válida.",

            _ =>
                $"Error {(int)response.StatusCode} al llamar a la API."
        };
    }

    /// <summary>
    /// Refit lanza ApiException en métodos que devuelven objetos directos. En
    /// los endpoints que devuelven IApiResponse no lanza, pero deja el mismo
    /// ApiException en response.Error. Este método sirve para ambos casos.
    /// </summary>
    public static string Describe(
        Exception exception)
    {
        if (exception is not ApiException apiException)
            return exception.Message;

        var content = apiException.Content?.Trim();

        if (!string.IsNullOrWhiteSpace(content))
        {
            // /api/render y /api/bundles devuelven una lista de errores de
            // validación con código, path y mensaje.
            if (content.StartsWith('['))
            {
                var validation = TryExtractValidationErrors(content);

                if (!string.IsNullOrWhiteSpace(validation))
                    return validation;
            }

            if (!content.StartsWith('{') &&
                !content.StartsWith('<') &&
                !content.StartsWith('['))
            {
                return TrimQuotes(content);
            }

            var extracted = TryExtractProblemMessage(content);

            if (!string.IsNullOrWhiteSpace(extracted))
                return extracted;
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

    private static string TrimQuotes(
        string text) =>
        text.Length >= 2 &&
        text[0] == '"' &&
        text[^1] == '"'
            ? text[1..^1]
            : text;

    /// <summary>
    /// [{ "code", "message", "path" }, ...] → "path: mensaje · path: mensaje".
    /// </summary>
    private static string? TryExtractValidationErrors(
        string content)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(content);

            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return null;

            var messages = new List<string>();

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                    continue;

                var message =
                    ReadString(item, "message") ?? ReadString(item, "Message");

                if (string.IsNullOrWhiteSpace(message))
                    continue;

                var path =
                    ReadString(item, "path") ?? ReadString(item, "Path");

                messages.Add(
                    string.IsNullOrWhiteSpace(path)
                        ? message
                        : $"{path}: {message}");
            }

            return messages.Count == 0
                ? null
                : string.Join(" · ", messages);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadString(
        System.Text.Json.JsonElement element,
        string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TryExtractProblemMessage(
        string content)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(content);
            var root = document.RootElement;

            foreach (var property in new[] { "detail", "title", "message", "error" })
            {
                if (root.TryGetProperty(property, out var value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch
        {
            // El cuerpo no era JSON problem-details. Se usa el fallback.
        }

        return null;
    }
}
